using System;
using System.Linq;
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
    /// <summary>The list of creators for fans to find and follow.</summary>
    [Collection(IntegrationTestCollection.Definition)]
    public class CreatorDirectoryTests : ServiceIntegrationTest
    {
        public CreatorDirectoryTests(ITestOutputHelper testOutputHelper, MongoDbFixture mongoDb) : base(
            testOutputHelper, mongoDb)
        {
        }

        private FakeVideoConverter Converter => Factory.Services.GetRequiredService<FakeVideoConverter>();

        /// <summary>A word that only the creators of one test have: the database is shared with the other tests</summary>
        private readonly string _tag = "Zq" + Guid.NewGuid().ToString("N")[..8];

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

        private async Task<(UserAccount host, string creatorId)> NewCreator(string name)
        {
            var host = Factory.CreateUser("host" + Guid.NewGuid().ToString("N")[..6], false);
            var profile = await Read<CreatorProfileDto>(await Api(host).PutAsync("/v1/creators/me",
                new JsonNetContent(new SaveCreatorDto($"{name} {_tag}", "Teaches piano", "058", "0123456789"))));
            return (host, profile.CreatorId);
        }

        private UserAccount NewFan() => Factory.CreateUser("fan" + Guid.NewGuid().ToString("N")[..6], false);

        private async Task ReadyVideo(UserAccount host)
        {
            var created = await Read<CreatedVideoDto>(await Api(host).PostAsync("/v1/videos",
                new JsonNetContent(new CreateVideoDto("A reel by " + host.Name, null, 50_000_000))));
            var id = created.Video.VideoId;
            Converter.Receive(id);
            (await Api(host).PostAsync($"/v1/videos/{id}/uploaded", null)).EnsureSuccessStatusCode();
            Converter.Finish(id);
            AssertSuccess(await Factory.Services.GetRequiredService<MediatR.IMediator>()
                .Send(new ProcessVideosRequest(id)));
        }

        private async Task<CreatorCardListDto> Discover(UserAccount? viewer, string query = "")
        {
            var url = "/v1/creators/discover?limit=60&q=" + Uri.EscapeDataString(_tag + " " + query);
            return await Read<CreatorCardListDto>(await Api(viewer).GetAsync(url));
        }

        [Fact]
        public async Task OnlyCreatorsWithSomethingToSee_AreListed()
        {
            var (withVideo, idWithVideo) = await NewCreator("Has video");
            var (withSession, idWithSession) = await NewCreator("Has session");
            var (_, idEmpty) = await NewCreator("Has nothing");
            await ReadyVideo(withVideo);
            (await Api(withSession).PostAsync("/v1/sessions", new JsonNetContent(new CreateSessionDto("Jazz night",
                "Live", "music", 500000, DateTimeOffset.UtcNow.AddDays(2))))).EnsureSuccessStatusCode();

            var list = await Discover(null);

            var ids = list.Items.Select(x => x.Creator.CreatorId).ToList();
            Assert.Contains(idWithVideo, ids);
            Assert.Contains(idWithSession, ids);
            Assert.DoesNotContain(idEmpty, ids);
            Assert.Equal(2, list.Total);
            var card = list.Items.Single(x => x.Creator.CreatorId == idWithVideo);
            Assert.Equal(1, card.Videos);
            Assert.NotNull(card.CoverUrl);
            Assert.Equal(1, list.Items.Single(x => x.Creator.CreatorId == idWithSession).Sessions);
            Assert.Null(list.Items.Single(x => x.Creator.CreatorId == idWithSession).CoverUrl);
        }

        [Fact]
        public async Task Search_FindsByNameAndBio_WithAllWords()
        {
            var (host, id) = await NewCreator("Piano Queen");
            await ReadyVideo(host);
            var (other, otherId) = await NewCreator("Drum King");
            await ReadyVideo(other);

            Assert.Equal(new[] {id}, (await Discover(null, "queen")).Items.Select(x => x.Creator.CreatorId).ToArray());
            Assert.Equal(new[] {id}, (await Discover(null, "PIANO queen")).Items.Select(x => x.Creator.CreatorId).ToArray());
            // the bio says "Teaches piano" for both
            Assert.Equal(2, (await Discover(null, "teaches")).Total);
            Assert.Empty((await Discover(null, "queen king")).Items);
            Assert.Contains(otherId, (await Discover(null, "")).Items.Select(x => x.Creator.CreatorId));
        }

        [Fact]
        public async Task Popular_PutsTheMostFollowedFirst_New_TheNewestFirst()
        {
            var (hostA, idA) = await NewCreator("First made");
            await ReadyVideo(hostA);
            await Task.Delay(20);
            var (hostB, idB) = await NewCreator("Second made");
            await ReadyVideo(hostB);
            // A has the followers, B is the newest
            await Api(NewFan()).PutAsync($"/v1/creators/{idA}/follow", null);
            await Api(NewFan()).PutAsync($"/v1/creators/{idA}/follow", null);
            await Api(NewFan()).PutAsync($"/v1/creators/{idB}/follow", null);

            var popular = await Read<CreatorCardListDto>(await Api(null).GetAsync(
                $"/v1/creators/discover?sort=popular&q={_tag}"));
            var newest = await Read<CreatorCardListDto>(await Api(null).GetAsync(
                $"/v1/creators/discover?sort=new&q={_tag}"));

            Assert.Equal(new[] {idA, idB}, popular.Items.Select(x => x.Creator.CreatorId).ToArray());
            Assert.Equal(new[] {idB, idA}, newest.Items.Select(x => x.Creator.CreatorId).ToArray());
            Assert.Equal(2, popular.Items[0].Followers);
        }

        [Fact]
        public async Task TheList_SaysWhomThePersonFollows_AndWhichCreatorIsThem()
        {
            var (host, id) = await NewCreator("Followed");
            await ReadyVideo(host);
            var (_, otherId) = await NewCreator("Not followed");
            var fan = NewFan();
            await Api(fan).PutAsync($"/v1/creators/{id}/follow", null);
            await ReadyVideo((await NewCreator("Third")).host);

            var asFan = await Discover(fan);
            Assert.True(asFan.Items.Single(x => x.Creator.CreatorId == id).Following);
            Assert.All(asFan.Items.Where(x => x.Creator.CreatorId != id), x => Assert.False(x.Following));
            Assert.All(asFan.Items, x => Assert.False(x.IsYou));
            Assert.All((await Discover(null)).Items, x => Assert.False(x.Following));
            Assert.True((await Discover(host)).Items.Single(x => x.Creator.CreatorId == id).IsYou);
            Assert.DoesNotContain(otherId, asFan.Items.Select(x => x.Creator.CreatorId));
        }

        [Fact]
        public async Task Following_ListsOnlyTheFollowed_EvenWithoutAnyPost_AndNothingWhenSignedOut()
        {
            var (host, id) = await NewCreator("Quiet one");
            var fan = NewFan();
            await Api(fan).PutAsync($"/v1/creators/{id}/follow", null);
            await ReadyVideo((await NewCreator("Busy one")).host);

            var followed = await Read<CreatorCardListDto>(await Api(fan).GetAsync(
                $"/v1/creators/discover?following=true&q={_tag}"));
            var signedOut = await Read<CreatorCardListDto>(await Api(null).GetAsync(
                $"/v1/creators/discover?following=true&q={_tag}"));

            Assert.Equal(new[] {id}, followed.Items.Select(x => x.Creator.CreatorId).ToArray());
            Assert.True(followed.Items[0].Following);
            Assert.Empty(signedOut.Items);
        }

        [Fact]
        public async Task Paging_GivesPagesInOrder_AndTheTotal()
        {
            for (var i = 0; i < 3; i++)
            {
                await ReadyVideo((await NewCreator("Creator " + i)).host);
                await Task.Delay(15);
            }

            var first = await Read<CreatorCardListDto>(await Api(null).GetAsync(
                $"/v1/creators/discover?sort=new&limit=2&skip=0&q={_tag}"));
            var second = await Read<CreatorCardListDto>(await Api(null).GetAsync(
                $"/v1/creators/discover?sort=new&limit=2&skip=2&q={_tag}"));

            Assert.Equal(3, first.Total);
            Assert.Equal(2, first.Items.Length);
            Assert.Single(second.Items);
            Assert.Empty(first.Items.Select(x => x.Creator.CreatorId).Intersect(second.Items.Select(x => x.Creator.CreatorId)));
        }

        [Fact]
        public async Task NoBankDetailsAreInTheList()
        {
            var (host, _) = await NewCreator("Private bank");
            await ReadyVideo(host);

            var response = await Api(null).GetAsync($"/v1/creators/discover?q={_tag}");
            var text = await response.Content.ReadAsStringAsync();

            Assert.DoesNotContain("0123456789", text);
            Assert.DoesNotContain("accountLast4", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("bankCode", text, StringComparison.OrdinalIgnoreCase);
        }
    }
}
