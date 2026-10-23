using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Strive.Core.Services.Videos;
using Strive.Infrastructure.Serialization;
using Strive.IntegrationTests._Helpers;
using Strive.Models.Response;
using Xunit;
using Xunit.Abstractions;

namespace Strive.IntegrationTests.Services
{
    /// <summary>Following creators: counted on their page, listed for the follower, and a feed of their videos only.</summary>
    [Collection(IntegrationTestCollection.Definition)]
    public class FollowsTests : ServiceIntegrationTest
    {
        public FollowsTests(ITestOutputHelper testOutputHelper, MongoDbFixture mongoDb) : base(testOutputHelper,
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

        private static async Task<T> Read<T>(HttpResponseMessage response)
        {
            response.EnsureSuccessStatusCode();
            return JsonConvert.DeserializeObject<T>(await response.Content.ReadAsStringAsync(), JsonConfig.Default)!;
        }

        private async Task<(UserAccount user, string creatorId)> NewCreator()
        {
            var host = Factory.CreateUser("host" + Guid.NewGuid().ToString("N")[..6], false);
            var profile = await Read<CreatorProfileDto>(await Api(host).PutAsync("/v1/creators/me",
                new JsonNetContent(new SaveCreatorDto("Ada Lovelace", "Piano", "058", "0123456789"))));
            return (host, profile.CreatorId);
        }

        private UserAccount NewFan() => Factory.CreateUser("fan" + Guid.NewGuid().ToString("N")[..6], false);

        private Task<HttpResponseMessage> Follow(UserAccount? user, string creatorId) =>
            Api(user).PutAsync($"/v1/creators/{creatorId}/follow", null);

        private Task<HttpResponseMessage> Unfollow(UserAccount? user, string creatorId) =>
            Api(user).DeleteAsync($"/v1/creators/{creatorId}/follow");

        private async Task<CreatorPageDto> Page(UserAccount? user, string creatorId) =>
            await Read<CreatorPageDto>(await Api(user).GetAsync($"/v1/creators/{creatorId}/page"));

        private async Task<string> ReadyVideo(UserAccount host)
        {
            var created = await Read<CreatedVideoDto>(await Api(host).PostAsync("/v1/videos",
                new JsonNetContent(new CreateVideoDto("A reel by " + host.Name, null, 50_000_000))));
            var id = created.Video.VideoId;
            Converter.Receive(id);
            (await Api(host).PostAsync($"/v1/videos/{id}/uploaded", null)).EnsureSuccessStatusCode();
            Converter.Finish(id);
            AssertSuccess(await Factory.Services.GetRequiredService<MediatR.IMediator>()
                .Send(new ProcessVideosRequest(id)));
            return id;
        }

        [Fact]
        public async Task Follow_IsCounted_OnThePage_AndKnownToTheFollower()
        {
            var (_, creatorId) = await NewCreator();
            var fan = NewFan();
            var other = NewFan();

            (await Follow(fan, creatorId)).EnsureSuccessStatusCode();
            await Follow(other, creatorId);

            var asFan = await Page(fan, creatorId);
            Assert.Equal(2, asFan.Followers);
            Assert.True(asFan.Following);

            var anonymous = await Page(null, creatorId);
            Assert.Equal(2, anonymous.Followers);
            Assert.False(anonymous.Following);
        }

        [Fact]
        public async Task Follow_Twice_CountsOnce_AndUnfollowTwiceIsFine()
        {
            var (_, creatorId) = await NewCreator();
            var fan = NewFan();

            (await Follow(fan, creatorId)).EnsureSuccessStatusCode();
            (await Follow(fan, creatorId)).EnsureSuccessStatusCode();
            Assert.Equal(1, (await Page(null, creatorId)).Followers);

            (await Unfollow(fan, creatorId)).EnsureSuccessStatusCode();
            (await Unfollow(fan, creatorId)).EnsureSuccessStatusCode();
            var after = await Page(fan, creatorId);
            Assert.Equal(0, after.Followers);
            Assert.False(after.Following);
        }

        [Fact]
        public async Task Follow_NeedsASignIn_ACreatorThatIsThere_AndNotYourself()
        {
            var (host, creatorId) = await NewCreator();

            Assert.Equal(HttpStatusCode.Unauthorized, (await Follow(null, creatorId)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Follow(NewFan(), "nobody")).StatusCode);
            var own = await Follow(host, creatorId);
            Assert.Equal(HttpStatusCode.BadRequest, own.StatusCode);
            Assert.Contains("Social_Invalid", await own.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Following_ListsTheCreators_LatestFirst()
        {
            var (_, first) = await NewCreator();
            var (_, second) = await NewCreator();
            var fan = NewFan();
            await Follow(fan, first);
            await Task.Delay(20);
            await Follow(fan, second);

            var list = await Read<PublicCreatorDto[]>(await Api(fan).GetAsync("/v1/creators/following"));

            Assert.Equal(new[] {second, first}, list.Select(x => x.CreatorId).ToArray());
            Assert.Empty(await Read<PublicCreatorDto[]>(await Api(NewFan()).GetAsync("/v1/creators/following")));
            Assert.Equal(HttpStatusCode.Unauthorized, (await Api(null).GetAsync("/v1/creators/following")).StatusCode);
        }

        [Fact]
        public async Task Feed_OfFollowed_HasOnlyTheirVideos_AndNothingWhenSignedOut()
        {
            var (hostA, creatorA) = await NewCreator();
            var (hostB, _) = await NewCreator();
            var idA = await ReadyVideo(hostA);
            var idB = await ReadyVideo(hostB);
            var fan = NewFan();
            await Follow(fan, creatorA);

            var followed = await Read<VideoFeedDto>(await Api(fan).GetAsync("/v1/videos/feed?following=true&limit=30"));
            var everything = await Read<VideoFeedDto>(await Api(fan).GetAsync("/v1/videos/feed?limit=30"));
            var signedOut = await Read<VideoFeedDto>(await Api(null).GetAsync("/v1/videos/feed?following=true"));

            Assert.Equal(new[] {idA}, followed.Items.Select(x => x.VideoId).Where(x => x == idA || x == idB).ToArray());
            Assert.DoesNotContain(followed.Items, x => x.VideoId == idB);
            Assert.Contains(everything.Items, x => x.VideoId == idB);
            Assert.Empty(signedOut.Items);
        }

        [Fact]
        public async Task Feed_OfFollowed_WithNoFollows_IsEmpty()
        {
            var (host, _) = await NewCreator();
            await ReadyVideo(host);

            var feed = await Read<VideoFeedDto>(await Api(NewFan()).GetAsync("/v1/videos/feed?following=true"));

            Assert.Empty(feed.Items);
        }
    }
}
