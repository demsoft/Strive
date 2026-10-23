using System;
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
    /// <summary>Likes on videos: one per person, counted everywhere the video is shown.</summary>
    [Collection(IntegrationTestCollection.Definition)]
    public class LikesTests : ServiceIntegrationTest
    {
        public LikesTests(ITestOutputHelper testOutputHelper, MongoDbFixture mongoDb) : base(testOutputHelper, mongoDb)
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

        private static async Task<T> Read<T>(HttpResponseMessage response)
        {
            response.EnsureSuccessStatusCode();
            return JsonConvert.DeserializeObject<T>(await response.Content.ReadAsStringAsync(), JsonConfig.Default)!;
        }

        private UserAccount NewFan() => Factory.CreateUser("fan" + Guid.NewGuid().ToString("N")[..6], false);

        private async Task<(UserAccount host, string creatorId, string videoId)> ReadyVideo()
        {
            var host = Factory.CreateUser("host" + Guid.NewGuid().ToString("N")[..6], false);
            var profile = await Read<CreatorProfileDto>(await Api(host).PutAsync("/v1/creators/me",
                new JsonNetContent(new SaveCreatorDto("Ada Lovelace", "Piano", "058", "0123456789"))));
            var created = await Read<CreatedVideoDto>(await Api(host).PostAsync("/v1/videos",
                new JsonNetContent(new CreateVideoDto("A reel by " + host.Name, null, 50_000_000))));
            var id = created.Video.VideoId;
            Converter.Receive(id);
            (await Api(host).PostAsync($"/v1/videos/{id}/uploaded", null)).EnsureSuccessStatusCode();
            Converter.Finish(id);
            AssertSuccess(await Factory.Services.GetRequiredService<MediatR.IMediator>()
                .Send(new ProcessVideosRequest(id)));
            return (host, profile.CreatorId, id);
        }

        private Task<HttpResponseMessage> Like(UserAccount? user, string id) =>
            Api(user).PutAsync($"/v1/videos/{id}/like", null);

        private Task<HttpResponseMessage> Unlike(UserAccount? user, string id) =>
            Api(user).DeleteAsync($"/v1/videos/{id}/like");

        private async Task<VideoDto> Watch(UserAccount? user, string id) =>
            await Read<VideoDto>(await Api(user).GetAsync($"/v1/videos/{id}"));

        [Fact]
        public async Task Like_IsCounted_AndTheLikerSeesThatTheyLikeIt()
        {
            var (_, _, id) = await ReadyVideo();
            var fan = NewFan();
            var other = NewFan();

            var state = await Read<LikeStateDto>(await Like(fan, id));
            await Like(other, id);

            Assert.Equal(1, state.Likes);
            Assert.True(state.Liked);
            var asFan = await Watch(fan, id);
            Assert.Equal(2, asFan.Likes);
            Assert.True(asFan.Liked);
            var anonymous = await Watch(null, id);
            Assert.Equal(2, anonymous.Likes);
            Assert.False(anonymous.Liked);
        }

        [Fact]
        public async Task Like_Twice_CountsOnce_AndUnlikeTwiceIsFine()
        {
            var (_, _, id) = await ReadyVideo();
            var fan = NewFan();

            await Like(fan, id);
            var again = await Read<LikeStateDto>(await Like(fan, id));
            Assert.Equal(1, again.Likes);

            var first = await Read<LikeStateDto>(await Unlike(fan, id));
            var second = await Read<LikeStateDto>(await Unlike(fan, id));
            Assert.Equal(0, first.Likes);
            Assert.False(first.Liked);
            Assert.Equal(0, second.Likes);
            Assert.Equal(0, (await Watch(fan, id)).Likes);
        }

        [Fact]
        public async Task Like_NeedsASignIn_AndAVideoThatIsShown()
        {
            var (_, _, id) = await ReadyVideo();

            Assert.Equal(HttpStatusCode.Unauthorized, (await Like(null, id)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Unlike(null, id)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Like(NewFan(), "nothing")).StatusCode);

            // hidden by reports: nobody can like it any more
            for (var i = 0; i < 3; i++)
                (await Api(NewFan()).PostAsync($"/v1/videos/{id}/report",
                    new JsonNetContent(new ReportVideoDto("spam", null)))).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.NotFound, (await Like(NewFan(), id)).StatusCode);
        }

        [Fact]
        public async Task Likes_AreInTheFeed_OnTheCreatorPage_AndInWhatTheCreatorSees()
        {
            var (host, creatorId, id) = await ReadyVideo();
            var fan = NewFan();
            await Like(fan, id);

            var feed = await Read<VideoFeedDto>(await Api(fan).GetAsync("/v1/videos/feed?limit=30"));
            var inFeed = feed.Items.Single(x => x.VideoId == id);
            Assert.Equal(1, inFeed.Likes);
            Assert.True(inFeed.Liked);

            var page = await Read<CreatorPageDto>(await Api(fan).GetAsync($"/v1/creators/{creatorId}/page"));
            Assert.Equal(1, page.Videos!.Single().Likes);

            var mine = await Read<VideoDto[]>(await Api(host).GetAsync("/v1/videos/mine"));
            Assert.Equal(1, mine.Single().Likes);
            Assert.False(mine.Single().Liked);
        }

        [Fact]
        public async Task Deleting_AVideo_TakesItsLikesWithIt()
        {
            var (host, _, id) = await ReadyVideo();
            await Like(NewFan(), id);

            (await Api(host).DeleteAsync($"/v1/videos/{id}")).EnsureSuccessStatusCode();

            var counts = await Factory.Services.GetRequiredService<IVideoLikeRepo>().CountsOf(new[] {id});
            Assert.Empty(counts);
        }
    }
}
