using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Strive.Core.Services.Social;
using Strive.Core.Services.Videos;
using Strive.Infrastructure.Serialization;
using Strive.IntegrationTests._Helpers;
using Strive.Models.Response;
using Xunit;
using Xunit.Abstractions;

namespace Strive.IntegrationTests.Services
{
    /// <summary>Comments under videos: who may write, read and take them away.</summary>
    [Collection(IntegrationTestCollection.Definition)]
    public class CommentsTests : ServiceIntegrationTest
    {
        public CommentsTests(ITestOutputHelper testOutputHelper, MongoDbFixture mongoDb) : base(testOutputHelper,
            mongoDb)
        {
        }

        private FakeVideoConverter Converter => Factory.Services.GetRequiredService<FakeVideoConverter>();

        private HttpClient Api(UserAccount? user)
        {
            var client = Factory.CreateClient();
            if (user != null)
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, user.Token);
            return client;
        }

        private HttpClient AdminApi()
        {
            var client = Factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                Factory.JwtTokens.GenerateJwtToken(new List<System.Security.Claims.Claim>
                {
                    new(System.Security.Claims.ClaimTypes.NameIdentifier, "admin-" + Guid.NewGuid().ToString("N")[..6]),
                    new("name", "Admin"), new("role", "serveradmin"),
                }));
            return client;
        }

        private static async Task<T> Read<T>(HttpResponseMessage response)
        {
            response.EnsureSuccessStatusCode();
            return JsonConvert.DeserializeObject<T>(await response.Content.ReadAsStringAsync(), JsonConfig.Default)!;
        }

        private UserAccount NewFan(string name = "Tola") => Factory.CreateUser(name, false);

        private async Task<(UserAccount host, string videoId)> ReadyVideo()
        {
            var host = Factory.CreateUser("host" + Guid.NewGuid().ToString("N")[..6], false);
            (await Api(host).PutAsync("/v1/creators/me",
                new JsonNetContent(new SaveCreatorDto("Ada Lovelace", "Piano", "058", "0123456789"))))
                .EnsureSuccessStatusCode();
            var created = await Read<CreatedVideoDto>(await Api(host).PostAsync("/v1/videos",
                new JsonNetContent(new CreateVideoDto("A reel by " + host.Name, null, 50_000_000))));
            var id = created.Video.VideoId;
            Converter.Receive(id);
            (await Api(host).PostAsync($"/v1/videos/{id}/uploaded", null)).EnsureSuccessStatusCode();
            Converter.Finish(id);
            AssertSuccess(await Factory.Services.GetRequiredService<MediatR.IMediator>()
                .Send(new ProcessVideosRequest(id)));
            return (host, id);
        }

        private Task<HttpResponseMessage> Post(UserAccount? user, string id, string? text) =>
            Api(user).PostAsync($"/v1/videos/{id}/comments", new JsonNetContent(new PostCommentDto(text)));

        private async Task<CommentListDto> List(UserAccount? user, string id, string query = "") =>
            await Read<CommentListDto>(await Api(user).GetAsync($"/v1/videos/{id}/comments{query}"));

        [Fact]
        public async Task Post_ShowsUnderTheVideo_WithTheNameOfTheAuthor_NewestFirst()
        {
            var (_, id) = await ReadyVideo();
            var fan = NewFan("Tola");

            var first = await Read<CommentDto>(await Post(fan, id, "  First!  "));
            await Task.Delay(20);
            await Post(NewFan("Bayo"), id, "Second");

            Assert.Equal("First!", first.Text);
            Assert.Equal("Tola", first.AuthorName);
            Assert.True(first.Mine);

            var list = await List(null, id);
            Assert.Equal(new[] {"Second", "First!"}, list.Items.Select(x => x.Text).ToArray());
            Assert.Equal(2, list.Total);
            Assert.All(list.Items, x => Assert.False(x.Mine || x.CanDelete));
        }

        [Fact]
        public async Task List_PagesBackwards()
        {
            var (_, id) = await ReadyVideo();
            for (var i = 1; i <= 3; i++)
            {
                await Post(NewFan(), id, "Comment " + i);
                await Task.Delay(15);
            }

            var page = await List(null, id, "?limit=2");
            Assert.Equal(new[] {"Comment 3", "Comment 2"}, page.Items.Select(x => x.Text).ToArray());
            Assert.NotNull(page.Next);

            var older = await List(null, id, "?limit=2&before=" + Uri.EscapeDataString(page.Next!.Value.ToString("o")));
            Assert.Equal(new[] {"Comment 1"}, older.Items.Select(x => x.Text).ToArray());
            Assert.Null(older.Next);
        }

        [Fact]
        public async Task Post_NeedsASignIn_SomethingWritten_AndAVideoThatIsShown()
        {
            var (_, id) = await ReadyVideo();

            Assert.Equal(HttpStatusCode.Unauthorized, (await Post(null, id, "Hi")).StatusCode);
            var empty = await Post(NewFan(), id, "   ");
            Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
            Assert.Contains("Social_InvalidComment", await empty.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(NewFan(), id, new string('x', 501))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Post(NewFan(), "nothing", "Hi")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Api(null).GetAsync("/v1/videos/nothing/comments")).StatusCode);

            for (var i = 0; i < 3; i++)
                (await Api(NewFan()).PostAsync($"/v1/videos/{id}/report",
                    new JsonNetContent(new ReportVideoDto("spam", null)))).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.NotFound, (await Post(NewFan(), id, "Hi")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Api(null).GetAsync($"/v1/videos/{id}/comments")).StatusCode);
        }

        [Fact]
        public async Task Post_ManyInAMinute_IsStopped()
        {
            var (_, id) = await ReadyVideo();
            var spammer = NewFan();

            for (var i = 0; i < CommentRules.MaxPerMinute; i++)
                (await Post(spammer, id, "Buy now " + i)).EnsureSuccessStatusCode();
            var response = await Post(spammer, id, "Buy now!!!");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("Social_TooFast", await response.Content.ReadAsStringAsync());
            Assert.Equal(CommentRules.MaxPerMinute, (await List(null, id, "?limit=50")).Total);
        }

        [Fact]
        public async Task Delete_TheWriter_TheCreatorOfTheVideo_AndAnAdministratorCan_ASomebodyElseCannot()
        {
            var (host, id) = await ReadyVideo();
            var writer = NewFan();
            var stranger = NewFan();

            async Task<string> Written()
            {
                await Task.Delay(5);
                return (await Read<CommentDto>(await Post(writer, id, "Hello " + Guid.NewGuid().ToString("N")[..4])))
                    .CommentId;
            }

            var byWriter = await Written();
            var byCreator = await Written();
            var byAdmin = await Written();
            var kept = await Written();

            Assert.Equal(HttpStatusCode.NotFound,
                (await Api(stranger).DeleteAsync($"/v1/videos/{id}/comments/{kept}")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await Api(null).DeleteAsync($"/v1/videos/{id}/comments/{kept}")).StatusCode);
            // not under this video
            Assert.Equal(HttpStatusCode.NotFound,
                (await Api(writer).DeleteAsync($"/v1/videos/another/comments/{kept}")).StatusCode);

            (await Api(writer).DeleteAsync($"/v1/videos/{id}/comments/{byWriter}")).EnsureSuccessStatusCode();
            (await Api(host).DeleteAsync($"/v1/videos/{id}/comments/{byCreator}")).EnsureSuccessStatusCode();
            (await AdminApi().DeleteAsync($"/v1/videos/{id}/comments/{byAdmin}")).EnsureSuccessStatusCode();

            Assert.Equal(new[] {kept}, (await List(null, id)).Items.Select(x => x.CommentId).ToArray());
            // gone is gone
            Assert.Equal(HttpStatusCode.NotFound,
                (await Api(writer).DeleteAsync($"/v1/videos/{id}/comments/{byWriter}")).StatusCode);
        }

        [Fact]
        public async Task List_SaysWhatTheAskerMayTakeAway()
        {
            var (host, id) = await ReadyVideo();
            var writer = NewFan();
            var other = NewFan();
            await Post(writer, id, "Mine");
            await Post(other, id, "Theirs");

            var asWriter = (await List(writer, id)).Items.ToDictionary(x => x.Text);
            Assert.True(asWriter["Mine"].Mine && asWriter["Mine"].CanDelete);
            Assert.False(asWriter["Theirs"].Mine || asWriter["Theirs"].CanDelete);

            Assert.All((await List(host, id)).Items, x => Assert.True(x.CanDelete && !x.Mine));
            Assert.All((await List(null, id)).Items, x => Assert.False(x.CanDelete));
        }

        [Fact]
        public async Task TheNumberOfComments_IsOnTheVideo_AndInTheFeed()
        {
            var (_, id) = await ReadyVideo();
            await Post(NewFan(), id, "One");
            await Post(NewFan(), id, "Two");

            var video = await Read<VideoDto>(await Api(null).GetAsync($"/v1/videos/{id}"));
            var feed = await Read<VideoFeedDto>(await Api(null).GetAsync("/v1/videos/feed?limit=30"));

            Assert.Equal(2, video.Comments);
            Assert.Equal(2, feed.Items.Single(x => x.VideoId == id).Comments);
        }

        [Fact]
        public async Task Deleting_AVideo_TakesItsCommentsWithIt()
        {
            var (host, id) = await ReadyVideo();
            await Post(NewFan(), id, "One");

            (await Api(host).DeleteAsync($"/v1/videos/{id}")).EnsureSuccessStatusCode();

            Assert.Empty(await Factory.Services.GetRequiredService<IVideoCommentRepo>().CountsOf(new[] {id}));
        }
    }
}
