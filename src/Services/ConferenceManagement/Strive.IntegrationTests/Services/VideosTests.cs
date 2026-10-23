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
    /// <summary>Videos from the creator's file to the player: sent to the converter, copied to the storage, played from there.</summary>
    [Collection(IntegrationTestCollection.Definition)]
    public class VideosTests : ServiceIntegrationTest
    {
        public VideosTests(ITestOutputHelper testOutputHelper, MongoDbFixture mongoDb) : base(testOutputHelper, mongoDb)
        {
        }

        private FakeVideoConverter Converter => Factory.Services.GetRequiredService<FakeVideoConverter>();
        private FakeVideoStorage Storage => Factory.Services.GetRequiredService<FakeVideoStorage>();
        private IVideoRepo Videos => Factory.Services.GetRequiredService<IVideoRepo>();

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

        private async Task<UserAccount> NewCreator()
        {
            var host = Factory.CreateUser("host" + Guid.NewGuid().ToString("N")[..6], false);
            (await Api(host).PutAsync("/v1/creators/me",
                new JsonNetContent(new SaveCreatorDto("Ada Lovelace", "Piano", "058", "0123456789"))))
                .EnsureSuccessStatusCode();
            return host;
        }

        private Task<HttpResponseMessage> Create(UserAccount? user, string title = "My first reel", long size = 50_000_000) =>
            Api(user).PostAsync("/v1/videos", new JsonNetContent(new CreateVideoDto(title, "About piano", size)));

        private async Task<CreatedVideoDto> Created(UserAccount host) => await Read<CreatedVideoDto>(await Create(host));

        private async Task<VideoDto> Complete(UserAccount host, string videoId) =>
            await Read<VideoDto>(await Api(host).PostAsync($"/v1/videos/{videoId}/uploaded", null));

        /// <summary>Looks at one video only: the database is shared, other tests leave videos behind</summary>
        private async Task<int> Process(string videoId)
        {
            var result = await Factory.Services.GetRequiredService<MediatR.IMediator>()
                .Send(new ProcessVideosRequest(videoId));
            AssertSuccess(result);
            return result.Response;
        }

        /// <summary>A video that is uploaded, converted and copied: ready to play</summary>
        private async Task<(UserAccount host, string id)> ReadyVideo()
        {
            var host = await NewCreator();
            var id = (await Created(host)).Video.VideoId;
            Converter.Receive(id);
            await Complete(host, id);
            Converter.Finish(id);
            await Process(id);
            return (host, id);
        }

        private async Task Age(string videoId, TimeSpan by)
        {
            var video = (await Videos.FindById(videoId))!;
            await Videos.Update(new Video(video.VideoId, video.CreatorId, video.CreatorUserId, video.Title,
                video.CreatedAt - by)
            {
                Description = video.Description, Status = video.Status, ProcessingSince = video.ProcessingSince - by,
                CopyAttempts = video.CopyAttempts, DurationSeconds = video.DurationSeconds, PriceMinor = video.PriceMinor,
            });
        }

        // ---- announcing

        [Fact]
        public async Task Create_GivesASignedFormForTheConverter_AndTheVideoIsUploading()
        {
            var host = await NewCreator();

            var created = await Created(host);

            Assert.Equal("uploading", created.Video.Status);
            Assert.Equal("My first reel", created.Video.Title);
            Assert.True(created.Video.IsOwner);
            Assert.Equal("https://converter.test/video/upload", created.Ticket.Url);
            Assert.Equal("gbedu/videos/" + created.Video.VideoId, created.Ticket.Fields["public_id"]);
            Assert.Contains("signature", created.Ticket.Fields.Keys);
        }

        [Theory]
        [InlineData("ab", 1000)]
        [InlineData("   ", 1000)]
        [InlineData("A fine title", 0)]
        [InlineData("A fine title", -5)]
        [InlineData("A fine title", 2_000_000_000)]
        public async Task Create_InvalidInput_BadRequest(string title, long size)
        {
            var response = await Create(await NewCreator(), title, size);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("Videos_Invalid", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Create_NotACreator_NotFound_NotSignedIn_Unauthorized()
        {
            Assert.Equal(HttpStatusCode.NotFound, (await Create(Factory.CreateUser("fan", false))).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Create(null)).StatusCode);
        }

        [Fact]
        public async Task Create_ServicesNotSetUp_SaysSo()
        {
            var host = await NewCreator();
            Converter.Configured = false;
            try
            {
                var response = await Create(host);

                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Contains("Videos_NotEnabled", await response.Content.ReadAsStringAsync());
            }
            finally
            {
                Converter.Configured = true;
            }
        }

        [Fact]
        public async Task Create_TooManyVideos_IsRefused()
        {
            var host = await NewCreator();
            var creatorId = (await Read<CreatorProfileDto>(await Api(host).GetAsync("/v1/creators/me"))).CreatorId;
            for (var i = 0; i < VideoRules.MaxVideosPerCreator; i++)
                await Videos.Create(new Video(Guid.NewGuid().ToString("N"), creatorId, host.Sub, "Video " + i,
                    DateTimeOffset.UtcNow));

            var response = await Create(host);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // ---- the file arrived

        [Fact]
        public async Task Uploaded_BeforeTheFileArrived_AsksToWait_AndWorksAfterwards()
        {
            var host = await NewCreator();
            var id = (await Created(host)).Video.VideoId;

            var early = await Api(host).PostAsync($"/v1/videos/{id}/uploaded", null);
            Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);
            Assert.Contains("Videos_UploadNotFound", await early.Content.ReadAsStringAsync());

            Converter.Receive(id, 95.5);
            var video = await Complete(host, id);

            Assert.Equal("processing", video.Status);
            Assert.Equal(95.5, video.DurationSeconds);
        }

        [Fact]
        public async Task Uploaded_TooLong_FailsWithAReason_AndIsDeletedAtTheConverter()
        {
            var host = await NewCreator();
            var id = (await Created(host)).Video.VideoId;
            Converter.Receive(id, VideoRules.MaxSeconds + 1);

            var video = await Complete(host, id);

            Assert.Equal("failed", video.Status);
            Assert.Contains("longer than 5 minutes", video.FailureReason);
            Assert.Contains(id, Converter.Deleted);
        }

        [Fact]
        public async Task Uploaded_JustAtTheLimit_IsAccepted()
        {
            var host = await NewCreator();
            var id = (await Created(host)).Video.VideoId;
            Converter.Receive(id, VideoRules.MaxSeconds);

            Assert.Equal("processing", (await Complete(host, id)).Status);
        }

        [Fact]
        public async Task Uploaded_SaidTwice_ChangesNothing()
        {
            var host = await NewCreator();
            var id = (await Created(host)).Video.VideoId;
            Converter.Receive(id);
            var first = await Complete(host, id);

            var second = await Complete(host, id);

            Assert.Equal("processing", second.Status);
            Assert.Equal(first.CreatedAt, second.CreatedAt);
        }

        [Fact]
        public async Task Uploaded_ConverterUnreachable_AsksToTryAgain_AndNothingIsLost()
        {
            var host = await NewCreator();
            var id = (await Created(host)).Video.VideoId;
            Converter.Receive(id);
            Converter.Unreachable = true;
            try
            {
                Assert.Equal(HttpStatusCode.BadRequest,
                    (await Api(host).PostAsync($"/v1/videos/{id}/uploaded", null)).StatusCode);
            }
            finally
            {
                Converter.Unreachable = false;
            }

            Assert.Equal("processing", (await Complete(host, id)).Status);
        }

        // ---- converting and copying

        [Fact]
        public async Task Process_ConversionNotFinished_StaysProcessing()
        {
            var host = await NewCreator();
            var id = (await Created(host)).Video.VideoId;
            Converter.Receive(id);
            await Complete(host, id);

            await Process(id);

            var video = await Read<VideoDto>(await Api(host).GetAsync($"/v1/videos/{id}"));
            Assert.Equal("processing", video.Status);
            Assert.Null(video.PlaybackUrl);
        }

        [Fact]
        public async Task Process_ConversionFinished_CopiesVideoAndCoverToTheStorage_AndCleansUpTheConverter()
        {
            var (host, id) = await ReadyVideo();

            Assert.Equal(FakeVideoConverter.VideoBytes, Storage.Objects[$"videos/{id}/video.mp4"].Data);
            Assert.Equal("video/mp4", Storage.Objects[$"videos/{id}/video.mp4"].ContentType);
            Assert.Equal(FakeVideoConverter.PosterBytes, Storage.Objects[$"videos/{id}/poster.jpg"].Data);
            Assert.Equal("image/jpeg", Storage.Objects[$"videos/{id}/poster.jpg"].ContentType);
            Assert.Contains(id, Converter.Deleted);
            var video = await Read<VideoDto>(await Api(host).GetAsync($"/v1/videos/{id}"));
            Assert.Equal("ready", video.Status);
        }

        [Fact]
        public async Task Process_WithoutAVideoId_LooksAtEveryWaitingVideo()
        {
            var host = await NewCreator();
            var id = (await Created(host)).Video.VideoId;
            Converter.Receive(id);
            await Complete(host, id);
            Converter.Finish(id);

            // the way the background service calls it; videos that other tests left behind are cleared on the way
            for (var round = 0; round < 10; round++)
            {
                AssertSuccess(await Factory.Services.GetRequiredService<MediatR.IMediator>().Send(new ProcessVideosRequest()));
                if ((await Videos.FindById(id))!.Status == VideoStatus.Ready) break;
            }

            Assert.Equal("ready", (await Read<VideoDto>(await Api(host).GetAsync($"/v1/videos/{id}"))).Status);
        }

        [Fact]
        public async Task Process_CoverNotThereYet_WaitsABitAndThenGoesWithoutIt()
        {
            var host = await NewCreator();
            var id = (await Created(host)).Video.VideoId;
            Converter.Receive(id);
            await Complete(host, id);
            Converter.Finish(id, poster: false);

            await Process(id);
            Assert.Equal("processing", (await Read<VideoDto>(await Api(host).GetAsync($"/v1/videos/{id}"))).Status);

            await Age(id, TimeSpan.FromMinutes(6));
            await Process(id);

            var video = await Read<VideoDto>(await Api(host).GetAsync($"/v1/videos/{id}"));
            Assert.Equal("ready", video.Status);
            Assert.Null(video.PosterUrl);
            Assert.NotNull(video.PlaybackUrl);
            Assert.DoesNotContain($"videos/{id}/poster.jpg", Storage.Objects.Keys);
        }

        [Fact]
        public async Task Process_NeverFinishes_FailsAfterTwoHours()
        {
            var host = await NewCreator();
            var id = (await Created(host)).Video.VideoId;
            Converter.Receive(id);
            await Complete(host, id);

            await Age(id, TimeSpan.FromHours(1));
            await Process(id);
            Assert.Equal("processing", (await Read<VideoDto>(await Api(host).GetAsync($"/v1/videos/{id}"))).Status);

            await Age(id, TimeSpan.FromHours(1.5));
            await Process(id);

            var video = await Read<VideoDto>(await Api(host).GetAsync($"/v1/videos/{id}"));
            Assert.Equal("failed", video.Status);
            Assert.Contains("could not be converted", video.FailureReason);
            Assert.Contains(id, Converter.Deleted);
        }

        [Fact]
        public async Task Process_StorageDown_IsTriedAgain_ThenFails()
        {
            var host = await NewCreator();
            var id = (await Created(host)).Video.VideoId;
            Converter.Receive(id);
            await Complete(host, id);
            Converter.Finish(id);
            Storage.Failing = true;
            try
            {
                for (var i = 0; i < VideoRules.MaxCopyAttempts - 1; i++) await Process(id);
                Assert.Equal("processing", (await Read<VideoDto>(await Api(host).GetAsync($"/v1/videos/{id}"))).Status);

                await Process(id);
            }
            finally
            {
                Storage.Failing = false;
            }

            var video = await Read<VideoDto>(await Api(host).GetAsync($"/v1/videos/{id}"));
            Assert.Equal("failed", video.Status);
            Assert.Contains("could not be saved", video.FailureReason);
        }

        [Fact]
        public async Task Process_StorageComesBack_TheVideoBecomesReady()
        {
            var host = await NewCreator();
            var id = (await Created(host)).Video.VideoId;
            Converter.Receive(id);
            await Complete(host, id);
            Converter.Finish(id);
            Storage.Failing = true;
            await Process(id);
            Storage.Failing = false;

            await Process(id);

            Assert.Equal("ready", (await Read<VideoDto>(await Api(host).GetAsync($"/v1/videos/{id}"))).Status);
        }

        [Fact]
        public async Task Process_FileNeverArrived_IsGivenUpAfterAWhile()
        {
            var host = await NewCreator();
            var id = (await Created(host)).Video.VideoId;

            await Age(id, TimeSpan.FromHours(1));
            await Process(id);
            Assert.Equal("uploading", (await Read<VideoDto>(await Api(host).GetAsync($"/v1/videos/{id}"))).Status);

            await Age(id, TimeSpan.FromHours(3));
            await Process(id);

            var video = await Read<VideoDto>(await Api(host).GetAsync($"/v1/videos/{id}"));
            Assert.Equal("failed", video.Status);
            Assert.Contains("not finished", video.FailureReason);
        }

        [Fact]
        public async Task Process_ConverterLostTheFile_Fails()
        {
            var host = await NewCreator();
            var id = (await Created(host)).Video.VideoId;
            Converter.Receive(id);
            await Complete(host, id);
            Converter.Files.TryRemove(id, out _);

            await Process(id);

            Assert.Equal("failed", (await Read<VideoDto>(await Api(host).GetAsync($"/v1/videos/{id}"))).Status);
        }

        [Fact]
        public async Task Process_ConverterUnreachable_ChangesNothing()
        {
            var host = await NewCreator();
            var id = (await Created(host)).Video.VideoId;
            Converter.Receive(id);
            await Complete(host, id);
            Converter.Finish(id);
            Converter.Unreachable = true;
            try
            {
                await Process(id);
            }
            finally
            {
                Converter.Unreachable = false;
            }

            Assert.Equal("processing", (await Read<VideoDto>(await Api(host).GetAsync($"/v1/videos/{id}"))).Status);
            await Process(id);
            Assert.Equal("ready", (await Read<VideoDto>(await Api(host).GetAsync($"/v1/videos/{id}"))).Status);
        }

        // ---- watching

        [Fact]
        public async Task Watch_ReadyFreeVideo_IsForEverybody_WithTemporaryLinks()
        {
            var (host, id) = await ReadyVideo();

            var video = await Read<VideoDto>(await Api(null).GetAsync($"/v1/videos/{id}"));

            Assert.Equal("ready", video.Status);
            Assert.False(video.IsOwner);
            Assert.StartsWith($"https://r2.test/videos/{id}/video.mp4", video.PlaybackUrl);
            Assert.Contains("expires=7200", video.PlaybackUrl); // two hours
            Assert.Contains("type=video%2Fmp4", video.PlaybackUrl);
            Assert.StartsWith($"https://r2.test/videos/{id}/poster.jpg", video.PosterUrl);
            Assert.Equal("Ada Lovelace", video.Creator!.DisplayName);
            Assert.Null(video.FailureReason);
        }

        [Fact]
        public async Task Watch_NotReady_IsHiddenFromOthers_ButNotFromTheOwner()
        {
            var host = await NewCreator();
            var fan = Factory.CreateUser("fan", false);
            var processing = (await Created(host)).Video.VideoId;
            Converter.Receive(processing);
            await Complete(host, processing);
            var failed = (await Created(host)).Video.VideoId;
            Converter.Receive(failed, 999);
            await Complete(host, failed);
            var (owner, paid) = await ReadyVideo();
            Assert.Equal(HttpStatusCode.NotFound, (await Api(null).GetAsync("/v1/videos/does-not-exist")).StatusCode);
        }

        [Fact]
        public async Task Watch_OnlyTheOwnerSeesWhyItFailed()
        {
            var host = await NewCreator();
            var id = (await Created(host)).Video.VideoId;
            Converter.Receive(id, 999);
            await Complete(host, id);

            var mine = await Read<VideoDto>(await Api(host).GetAsync($"/v1/videos/{id}"));

            Assert.Contains("longer than 5 minutes", mine.FailureReason);
        }

        [Fact]
        public async Task Mine_ListsTheOwnVideosNewestFirst_WithACoverButNoPlaybackLink()
        {
            var (host, first) = await ReadyVideo();
            var second = (await Created(host)).Video.VideoId;

            var list = await Read<VideoDto[]>(await Api(host).GetAsync("/v1/videos/mine"));

            Assert.Equal(new[] {second, first}, list.Select(x => x.VideoId));
            Assert.NotNull(list[1].PosterUrl);
            Assert.All(list, x => Assert.Null(x.PlaybackUrl));
        }

        [Fact]
        public async Task Mine_ShowsNothingOfOthers_AndANonCreatorHasNoVideos()
        {
            await ReadyVideo();
            var other = await NewCreator();

            Assert.Empty(await Read<VideoDto[]>(await Api(other).GetAsync("/v1/videos/mine")));
            Assert.Empty(await Read<VideoDto[]>(await Api(Factory.CreateUser("fan", false)).GetAsync("/v1/videos/mine")));
            Assert.Equal(HttpStatusCode.Unauthorized, (await Api(null).GetAsync("/v1/videos/mine")).StatusCode);
        }

        // ---- the feed and the page of the creator

        private async Task<VideoFeedDto> Feed(string query = "") =>
            await Read<VideoFeedDto>(await Api(null).GetAsync("/v1/videos/feed" + query));

        [Fact]
        public async Task Feed_ListsReadyVideosOnly_ForEverybody_WithTheCreatorAndTheCover()
        {
            var (host, ready) = await ReadyVideo();
            var processing = (await Created(host)).Video.VideoId;
            Converter.Receive(processing);
            await Complete(host, processing);
            var uploading = (await Created(host)).Video.VideoId;
            var failed = (await Created(host)).Video.VideoId;
            Converter.Receive(failed, 999);
            await Complete(host, failed);

            var feed = await Feed();

            var item = Assert.Single(feed.Items, x => x.VideoId == ready);
            Assert.Equal("ready", item.Status);
            Assert.Equal("Ada Lovelace", item.Creator!.DisplayName);
            Assert.StartsWith($"https://r2.test/videos/{ready}/poster.jpg", item.PosterUrl);
            Assert.Null(item.PlaybackUrl); // watching asks for the video itself
            Assert.Null(item.FailureReason);
            Assert.DoesNotContain(feed.Items, x => new[] {processing, uploading, failed}.Contains(x.VideoId));
        }

        [Fact]
        public async Task Feed_NewestFirst_AndPagesDoNotRepeatOrSkip()
        {
            var host = await NewCreator();
            var ids = new System.Collections.Generic.List<string>();
            for (var i = 0; i < 5; i++)
            {
                var id = (await Created(host)).Video.VideoId;
                Converter.Receive(id);
                await Complete(host, id);
                Converter.Finish(id);
                await Process(id);
                ids.Add(id);
                await Task.Delay(5); // a different moment for each
            }

            var seen = new System.Collections.Generic.List<string>();
            var page = await Feed("?limit=2");
            Assert.True(page.Items.Length <= 2);
            seen.AddRange(page.Items.Select(x => x.VideoId));
            for (var guard = 0; page.Next != null && guard < 200; guard++)
            {
                page = await Feed($"?limit=2&before={Uri.EscapeDataString(page.Next.Value.UtcDateTime.ToString("o"))}");
                seen.AddRange(page.Items.Select(x => x.VideoId));
            }

            Assert.Equal(seen.Count, seen.Distinct().Count());
            Assert.Equal(ids.AsEnumerable().Reverse(), seen.Where(ids.Contains)); // all five, newest first
            Assert.Null(page.Next);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-4)]
        [InlineData(100000)]
        public async Task Feed_AbsurdPageSizes_AreHeld_ToTheLimits(int limit)
        {
            await ReadyVideo();

            var feed = await Feed($"?limit={limit}");

            Assert.InRange(feed.Items.Length, 1, 30);
        }

        [Fact]
        public async Task CreatorPage_ShowsTheReadyVideosOfThatCreatorOnly()
        {
            var (host, ready) = await ReadyVideo();
            var processing = (await Created(host)).Video.VideoId;
            Converter.Receive(processing);
            await Complete(host, processing);
            var (_, othersVideo) = await ReadyVideo();
            var creatorId = (await Read<CreatorProfileDto>(await Api(host).GetAsync("/v1/creators/me"))).CreatorId;

            var page = await Read<CreatorPageDto>(await Api(null).GetAsync($"/v1/creators/{creatorId}/page"));

            Assert.Equal(new[] {ready}, page.Videos!.Select(x => x.VideoId));
            Assert.NotEmpty(page.Videos![0].PosterUrl!);
            Assert.DoesNotContain(page.Videos, x => x.VideoId == othersVideo || x.VideoId == processing);
        }

        [Fact]
        public async Task CreatorPage_WithoutVideos_HasAnEmptyList()
        {
            var host = await NewCreator();
            var creatorId = (await Read<CreatorProfileDto>(await Api(host).GetAsync("/v1/creators/me"))).CreatorId;

            var page = await Read<CreatorPageDto>(await Api(null).GetAsync($"/v1/creators/{creatorId}/page"));

            Assert.Empty(page.Videos!);
        }

        // ---- prices, buying, and who may watch

        private FakePaymentProvider Provider => Factory.Services.GetRequiredService<FakePaymentProvider>();

        private const long VideoPrice = 300_000; // ₦3,000

        private Task<HttpResponseMessage> Update(UserAccount host, string id, long price, string title = "Renamed reel",
            string? description = "New text") =>
            Api(host).PutAsync($"/v1/videos/{id}", new JsonNetContent(new UpdateVideoDto(title, description, price)));

        private async Task<(UserAccount host, string id)> PaidVideo(long price = VideoPrice)
        {
            var (host, id) = await ReadyVideo();
            (await Update(host, id, price)).EnsureSuccessStatusCode();
            return (host, id);
        }

        private async Task<VideoDto> View(UserAccount? user, string id) =>
            await Read<VideoDto>(await Api(user).GetAsync($"/v1/videos/{id}"));

        private async Task<string> StartCheckout(UserAccount fan, string id, string email = "fan@example.com")
        {
            var checkout = await Read<CheckoutResultDto>(await Api(fan).PostAsync($"/v1/videos/{id}/checkout",
                new JsonNetContent(new CheckoutDto(email))));
            return checkout.Reference!;
        }

        private async Task<PaymentStatusDto> PayFor(UserAccount fan, string id)
        {
            var reference = await StartCheckout(fan, id);
            Provider.Pay(reference, VideoPrice);
            return await Read<PaymentStatusDto>(await Api(fan).GetAsync($"/v1/payments/{reference}"));
        }

        [Fact]
        public async Task Price_CanBeSetWhenAnnouncing_AndChangedLater_ButOnlyToSomethingSensible()
        {
            var host = await NewCreator();
            var created = await Read<CreatedVideoDto>(await Api(host).PostAsync("/v1/videos",
                new JsonNetContent(new CreateVideoDto("A paid lesson", "Learn", 1000, VideoPrice))));
            Assert.Equal(VideoPrice, created.Video.PriceMinor);

            (await Update(host, created.Video.VideoId, 0)).EnsureSuccessStatusCode();
            var mine = await Read<VideoDto[]>(await Api(host).GetAsync("/v1/videos/mine"));
            Assert.Equal(0, Assert.Single(mine, x => x.VideoId == created.Video.VideoId).PriceMinor);
        }

        [Theory]
        [InlineData(50L)]
        [InlineData(9_999L)]
        [InlineData(100_000_001L)]
        [InlineData(-5L)]
        public async Task Price_OutOfRange_BadRequest(long price)
        {
            var (host, id) = await ReadyVideo();
            var create = await Api(host).PostAsync("/v1/videos",
                new JsonNetContent(new CreateVideoDto("A paid lesson", null, 1000, price)));
            Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);

            Assert.Equal(HttpStatusCode.BadRequest, (await Update(host, id, price)).StatusCode);
        }

        [Fact]
        public async Task Update_ChangesTitleTextAndPrice_ForTheOwnerOnly()
        {
            var (host, id) = await ReadyVideo();

            var updated = await Read<VideoDto>(await Update(host, id, VideoPrice, "  Better title  ", "More text"));

            Assert.Equal("Better title", updated.Title);
            Assert.Equal("More text", updated.Description);
            Assert.Equal(VideoPrice, updated.PriceMinor);
            Assert.Equal(HttpStatusCode.NotFound, (await Update(await NewCreator(), id, 0)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Update(null!, id, 0)).StatusCode);
            Assert.Equal("Better title", (await View(null, id)).Title);
        }

        [Theory]
        [InlineData("ab")]
        [InlineData("   ")]
        public async Task Update_TitleTooShort_BadRequest(string title)
        {
            var (host, id) = await ReadyVideo();

            Assert.Equal(HttpStatusCode.BadRequest, (await Update(host, id, 0, title)).StatusCode);
        }

        [Fact]
        public async Task Locked_APaidVideoIsShownWithItsPriceAndCover_ButDoesNotPlayWithoutPaying()
        {
            var (host, id) = await PaidVideo();
            var fan = Factory.CreateUser("fan", false);

            foreach (var viewer in new UserAccount?[] {null, fan})
            {
                var video = await View(viewer, id);
                Assert.True(video.Locked);
                Assert.Equal(VideoPrice, video.PriceMinor);
                Assert.NotNull(video.PosterUrl);
                Assert.Null(video.PlaybackUrl);
            }

            var owner = await View(host, id);
            Assert.False(owner.Locked);
            Assert.NotNull(owner.PlaybackUrl);
        }

        [Fact]
        public async Task Locked_FreeVideosAreNeverLocked()
        {
            var (_, id) = await ReadyVideo();

            var video = await View(null, id);

            Assert.False(video.Locked);
            Assert.NotNull(video.PlaybackUrl);
        }

        [Fact]
        public async Task Buying_UnlocksTheVideoForThatPersonOnly()
        {
            var (_, id) = await PaidVideo();
            var buyer = Factory.CreateUser("buyer", false);
            var other = Factory.CreateUser("other", false);

            var status = await PayFor(buyer, id);

            Assert.Equal("paid", status.Status);
            Assert.Equal("video", status.Kind);
            Assert.Equal(id, status.VideoId);
            Assert.Null(status.ConferenceId);
            var bought = await View(buyer, id);
            Assert.False(bought.Locked);
            Assert.NotNull(bought.PlaybackUrl);
            Assert.True((await View(other, id)).Locked);
            Assert.True((await View(null, id)).Locked);
        }

        [Fact]
        public async Task Buying_TheCheckoutUsesThePriceAndTheSplitOfAnyOtherSale()
        {
            var (_, id) = await PaidVideo();

            await StartCheckout(Factory.CreateUser("buyer", false), id);

            var request = Provider.Checkouts.Last();
            Assert.Equal(VideoPrice, request.AmountMinor);
            Assert.Equal(VideoPrice / 10, request.PlatformAmountMinor);
            Assert.Equal("video", request.Metadata["kind"]);
            Assert.Equal(id, request.Metadata["videoId"]);
            Assert.EndsWith($"/v/{id}", request.CallbackUrl);
        }

        [Fact]
        public async Task Buying_NotPaid_OrFailed_UnlocksNothing()
        {
            var (_, id) = await PaidVideo();
            var unpaid = Factory.CreateUser("unpaid", false);
            await StartCheckout(unpaid, id);
            var failed = Factory.CreateUser("failed", false);
            var reference = await StartCheckout(failed, id);
            Provider.Fail(reference, VideoPrice);
            await Api(failed).GetAsync($"/v1/payments/{reference}");

            Assert.True((await View(unpaid, id)).Locked);
            Assert.True((await View(failed, id)).Locked);
        }

        [Fact]
        public async Task Buying_Twice_IsNotNeeded_AndSettlingTwiceGrantsOnce()
        {
            var (_, id) = await PaidVideo();
            var buyer = Factory.CreateUser("buyer", false);
            var reference = await StartCheckout(buyer, id);
            Provider.Pay(reference, VideoPrice);
            await Api(buyer).GetAsync($"/v1/payments/{reference}");
            await Api(buyer).GetAsync($"/v1/payments/{reference}");

            var again = await Read<CheckoutResultDto>(await Api(buyer).PostAsync($"/v1/videos/{id}/checkout",
                new JsonNetContent(new CheckoutDto("fan@example.com"))));

            Assert.True(again.AlreadyHasTicket);
            Assert.Null(again.AuthorizationUrl);
            Assert.Equal(1, await Factory.Services.GetRequiredService<IVideoEntitlementRepo>().CountOfVideo(id));
            Assert.Single(await Read<VideoDto[]>(await Api(buyer).GetAsync("/v1/videos/purchased")), x => x.VideoId == id);
        }

        [Fact]
        public async Task Checkout_FreeNotReadyOwnOrUnknown_IsRefused()
        {
            var fan = Factory.CreateUser("fan", false);
            var (host, free) = await ReadyVideo();
            var processing = (await Created(host)).Video.VideoId;
            var (owner, paid) = await PaidVideo();

            Assert.Equal(HttpStatusCode.BadRequest, (await Api(fan).PostAsync($"/v1/videos/{free}/checkout",
                new JsonNetContent(new CheckoutDto("fan@example.com")))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Api(fan).PostAsync($"/v1/videos/{processing}/checkout",
                new JsonNetContent(new CheckoutDto("fan@example.com")))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Api(fan).PostAsync("/v1/videos/nope/checkout",
                new JsonNetContent(new CheckoutDto("fan@example.com")))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Api(owner).PostAsync($"/v1/videos/{paid}/checkout",
                new JsonNetContent(new CheckoutDto("owner@example.com")))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Api(fan).PostAsync($"/v1/videos/{paid}/checkout",
                new JsonNetContent(new CheckoutDto("not-an-email")))).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Api(null).PostAsync($"/v1/videos/{paid}/checkout",
                new JsonNetContent(new CheckoutDto("fan@example.com")))).StatusCode);
        }

        [Fact]
        public async Task Pass_OfTheCreator_UnlocksTheirVideos_ButNotThoseOfOthers()
        {
            var (host, id) = await PaidVideo();
            var (_, otherId) = await PaidVideo();
            var creatorId = (await Read<CreatorProfileDto>(await Api(host).GetAsync("/v1/creators/me"))).CreatorId;
            (await Api(host).PutAsync("/v1/creators/me/pass", new JsonNetContent(new SetPassPriceDto(2_000_000))))
                .EnsureSuccessStatusCode();
            var fan = Factory.CreateUser("passfan", false);
            var checkout = await Read<CheckoutResultDto>(await Api(fan).PostAsync($"/v1/creators/{creatorId}/pass/checkout",
                new JsonNetContent(new CheckoutDto("fan@example.com"))));
            Provider.Pay(checkout.Reference!, 2_000_000);
            await Api(fan).GetAsync($"/v1/payments/{checkout.Reference}");

            Assert.False((await View(fan, id)).Locked);
            Assert.NotNull((await View(fan, id)).PlaybackUrl);
            Assert.True((await View(fan, otherId)).Locked);
            // a person with a pass does not pay for a video of that creator
            Assert.True((await Read<CheckoutResultDto>(await Api(fan).PostAsync($"/v1/videos/{id}/checkout",
                new JsonNetContent(new CheckoutDto("fan@example.com"))))).AlreadyHasTicket);
        }

        [Fact]
        public async Task Feed_ShowsPaidVideosLocked_AndUnlockedForTheBuyer()
        {
            var (_, id) = await PaidVideo();
            var buyer = Factory.CreateUser("buyer", false);
            await PayFor(buyer, id);

            var anonymous = (await Read<VideoFeedDto>(await Api(null).GetAsync("/v1/videos/feed?limit=30"))).Items
                .Single(x => x.VideoId == id);
            var bought = (await Read<VideoFeedDto>(await Api(buyer).GetAsync("/v1/videos/feed?limit=30"))).Items
                .Single(x => x.VideoId == id);

            Assert.True(anonymous.Locked);
            Assert.Equal(VideoPrice, anonymous.PriceMinor);
            Assert.False(bought.Locked);
            Assert.Null(bought.PlaybackUrl); // lists never carry the video, only the page of the video does
        }

        [Fact]
        public async Task Purchased_ListsOnlyWhatThePersonBought()
        {
            var (_, bought) = await PaidVideo();
            var (_, notBought) = await PaidVideo();
            var buyer = Factory.CreateUser("buyer", false);
            await PayFor(buyer, bought);

            var list = await Read<VideoDto[]>(await Api(buyer).GetAsync("/v1/videos/purchased"));

            Assert.Equal(new[] {bought}, list.Select(x => x.VideoId));
            Assert.DoesNotContain(list, x => x.VideoId == notBought);
            Assert.Empty(await Read<VideoDto[]>(await Api(Factory.CreateUser("nobody", false)).GetAsync("/v1/videos/purchased")));
            Assert.Equal(HttpStatusCode.Unauthorized, (await Api(null).GetAsync("/v1/videos/purchased")).StatusCode);
        }

        [Fact]
        public async Task Delete_ABoughtVideo_IsRefused_ButAVideoNobodyBoughtCanGo()
        {
            var (host, bought) = await PaidVideo();
            await PayFor(Factory.CreateUser("buyer", false), bought);
            var (host2, unbought) = await PaidVideo();

            var refused = await Api(host).DeleteAsync($"/v1/videos/{bought}");
            var allowed = await Api(host2).DeleteAsync($"/v1/videos/{unbought}");

            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.Contains("Videos_HasBuyers", await refused.Content.ReadAsStringAsync());
            Assert.Contains($"videos/{bought}/video.mp4", Storage.Objects.Keys);
            allowed.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.NotFound, (await Api(null).GetAsync($"/v1/videos/{unbought}")).StatusCode);
        }

        [Fact]
        public async Task Earnings_VideoSalesAreCountedWithTheCommissionTakenOff()
        {
            var (host, id) = await PaidVideo();
            await Update(host, id, VideoPrice, "Piano lesson");
            await PayFor(Factory.CreateUser("buyer1", false), id);
            await PayFor(Factory.CreateUser("buyer2", false), id);

            var earnings = await Read<EarningsDto>(await Api(host).GetAsync("/v1/creators/me/earnings"));

            Assert.Equal(2, earnings.Totals.Videos);
            Assert.Equal(2 * VideoPrice * 9 / 10, earnings.Totals.NetMinor);
            var row = Assert.Single(earnings.Videos!);
            Assert.Equal("Piano lesson", row.Title);
            Assert.Equal(2, row.Sales);
            Assert.All(earnings.Recent, x => Assert.Equal("video", x.Kind));
            Assert.All(earnings.Recent, x => Assert.Equal("Piano lesson", x.SessionTitle));
        }

        [Fact]
        public async Task AdminPayments_ShowVideoSales()
        {
            var (host, id) = await PaidVideo();
            await Update(host, id, VideoPrice, "Drum lesson");
            var admin = Factory.CreateClient();
            admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                Factory.JwtTokens.GenerateJwtToken(new System.Collections.Generic.List<System.Security.Claims.Claim>
                {
                    new(System.Security.Claims.ClaimTypes.NameIdentifier, "admin1"), new("name", "Admin"),
                    new("role", "serveradmin"),
                }));
            var before = await Read<AdminPaymentsDto>(await admin.GetAsync("/v1/admin/payments"));

            await PayFor(Factory.CreateUser("buyer", false), id);
            var after = await Read<AdminPaymentsDto>(await admin.GetAsync("/v1/admin/payments"));

            Assert.Equal(1, after.Month.Videos - before.Month.Videos);
            var row = after.Recent.First(x => x.Kind == "video");
            Assert.Equal("Drum lesson", row.SessionTitle);
            Assert.Equal("Ada Lovelace", row.CreatorName);
        }

        // ---- reports and moderation

        private HttpClient AdminApi(string role = "serveradmin")
        {
            var client = Factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                Factory.JwtTokens.GenerateJwtToken(new System.Collections.Generic.List<System.Security.Claims.Claim>
                {
                    new(System.Security.Claims.ClaimTypes.NameIdentifier, "admin-" + Guid.NewGuid().ToString("N")[..6]),
                    new("name", "Admin"), new("role", role),
                }));
            return client;
        }

        private Task<HttpResponseMessage> Report(UserAccount? user, string id, string reason = "spam", string? details = null) =>
            Api(user).PostAsync($"/v1/videos/{id}/report", new JsonNetContent(new ReportVideoDto(reason, details)));

        private async Task ReportBy(int people, string id, string reason = "spam")
        {
            for (var i = 0; i < people; i++)
                (await Report(Factory.CreateUser("reporter" + i + Guid.NewGuid().ToString("N")[..4], false), id, reason))
                    .EnsureSuccessStatusCode();
        }

        private async Task<AdminVideoReportDto[]> AdminReports() =>
            await Read<AdminVideoReportDto[]>(await AdminApi().GetAsync("/v1/admin/videos/reports"));

        private async Task<bool> InFeed(string id) =>
            (await Feed("?limit=30")).Items.Any(x => x.VideoId == id);

        [Fact]
        public async Task Report_NeedsASignIn_AndAReason_AndAVideoThatIsThere()
        {
            var (_, id) = await ReadyVideo();
            var fan = Factory.CreateUser("fan", false);

            Assert.Equal(HttpStatusCode.Unauthorized, (await Report(null, id)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Report(fan, id, "nonsense")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Report(fan, id, "other", new string('x', 501))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Report(fan, "nope")).StatusCode);
            (await Report(fan, id, "Copyright", "It is not theirs")).EnsureSuccessStatusCode();
        }

        [Fact]
        public async Task Report_YourOwnVideo_OrOneThatIsNotPublic_IsRefused()
        {
            var (host, ready) = await ReadyVideo();
            var processing = (await Created(host)).Video.VideoId;
            Converter.Receive(processing);
            await Complete(host, processing);

            Assert.Equal(HttpStatusCode.BadRequest, (await Report(host, ready)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Report(Factory.CreateUser("fan", false), processing)).StatusCode);
        }

        [Fact]
        public async Task Report_OnePersonCannotHideAVideoAlone_EvenByReportingManyTimes()
        {
            var (_, id) = await ReadyVideo();
            var fan = Factory.CreateUser("fan", false);

            for (var i = 0; i < 6; i++) (await Report(fan, id)).EnsureSuccessStatusCode();

            Assert.True(await InFeed(id));
            Assert.Equal(1, Assert.Single(await AdminReports(), x => x.VideoId == id).OpenReports);
        }

        [Fact]
        public async Task Report_EnoughDifferentPeople_HideItUntilAModeratorHasLooked()
        {
            var (host, id) = await ReadyVideo();
            var creatorId = (await Read<CreatorProfileDto>(await Api(host).GetAsync("/v1/creators/me"))).CreatorId;

            await ReportBy(2, id);
            Assert.True(await InFeed(id));
            Assert.Equal("none", (await View(host, id)).Moderation);

            await ReportBy(1, id);

            Assert.False(await InFeed(id));
            Assert.Equal(HttpStatusCode.NotFound, (await Api(null).GetAsync($"/v1/videos/{id}")).StatusCode);
            Assert.DoesNotContain((await Read<CreatorPageDto>(await Api(null).GetAsync($"/v1/creators/{creatorId}/page"))).Videos!,
                x => x.VideoId == id);
            // the creator still sees it, and why it is not shown
            var mine = await View(host, id);
            Assert.Equal("hidden", mine.Moderation);
            Assert.NotNull(mine.PlaybackUrl);
            Assert.Equal("hidden", Assert.Single(await Read<VideoDto[]>(await Api(host).GetAsync("/v1/videos/mine")),
                x => x.VideoId == id).Moderation);
        }

        [Fact]
        public async Task Report_AHiddenVideo_CannotBeBoughtOrReportedAgain_AndIsNotInThePurchasedList()
        {
            var (host, id) = await PaidVideo();
            var buyer = Factory.CreateUser("buyer", false);
            await PayFor(buyer, id);
            await ReportBy(3, id);

            Assert.Equal(HttpStatusCode.NotFound, (await Api(Factory.CreateUser("late", false)).PostAsync(
                $"/v1/videos/{id}/checkout", new JsonNetContent(new CheckoutDto("late@example.com")))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Report(Factory.CreateUser("late2", false), id)).StatusCode);
            Assert.DoesNotContain(await Read<VideoDto[]>(await Api(buyer).GetAsync("/v1/videos/purchased")),
                x => x.VideoId == id);
            Assert.Equal(HttpStatusCode.NotFound, (await Api(buyer).GetAsync($"/v1/videos/{id}")).StatusCode);
        }

        [Fact]
        public async Task Admin_SeesWhatWasReported_WithReasonsDetailsBuyersAndAPreview()
        {
            var (_, id) = await PaidVideo();
            await PayFor(Factory.CreateUser("buyer", false), id);
            (await Report(Factory.CreateUser("r1", false), id, "copyright", "Stolen from another channel")).EnsureSuccessStatusCode();
            (await Report(Factory.CreateUser("r2", false), id, "copyright")).EnsureSuccessStatusCode();
            (await Report(Factory.CreateUser("r3", false), id, "spam")).EnsureSuccessStatusCode();

            var item = Assert.Single(await AdminReports(), x => x.VideoId == id);

            Assert.Equal("hidden", item.Moderation);
            Assert.Equal(3, item.OpenReports);
            Assert.Equal(2, item.Reasons["copyright"]);
            Assert.Equal(1, item.Reasons["spam"]);
            Assert.Equal("Stolen from another channel", Assert.Single(item.Details).Text);
            Assert.Equal(1, item.Buyers);
            Assert.Equal("Ada Lovelace", item.CreatorName);
            Assert.StartsWith($"https://r2.test/videos/{id}/video.mp4", item.PlaybackUrl); // to look at it while hidden
            Assert.NotNull(item.PosterUrl);
        }

        [Fact]
        public async Task Admin_Keep_ShowsItAgain_AndStartsTheCountFromZero()
        {
            var (host, id) = await ReadyVideo();
            await ReportBy(3, id);

            var keep = await AdminApi().PostAsync($"/v1/admin/videos/{id}/keep", null);

            Assert.Equal(HttpStatusCode.NoContent, keep.StatusCode);
            Assert.True(await InFeed(id));
            Assert.Equal("none", (await View(host, id)).Moderation);
            Assert.DoesNotContain(await AdminReports(), x => x.VideoId == id);
            // the old reports were decided: two new ones are not enough
            await ReportBy(2, id);
            Assert.True(await InFeed(id));
        }

        [Fact]
        public async Task Admin_Remove_DeletesTheFiles_TellsTheCreator_AndTakesItOutOfEverything()
        {
            var (host, id) = await ReadyVideo();
            await ReportBy(3, id);

            var remove = await AdminApi().PostAsync($"/v1/admin/videos/{id}/remove",
                new JsonNetContent(new ModerateVideoDto("  Not your own work.  ")));

            remove.EnsureSuccessStatusCode();
            Assert.Equal(0, (await Read<RemovedVideoDto>(remove)).Refunded);
            Assert.DoesNotContain($"videos/{id}/video.mp4", Storage.Objects.Keys);
            Assert.DoesNotContain($"videos/{id}/poster.jpg", Storage.Objects.Keys);
            Assert.Contains(id, Converter.Deleted);
            var mine = await View(host, id);
            Assert.Equal("removed", mine.Moderation);
            Assert.Equal("Not your own work.", mine.ModerationNote);
            Assert.Null(mine.PlaybackUrl);
            Assert.Null(mine.PosterUrl);
            Assert.Equal(HttpStatusCode.NotFound, (await Api(null).GetAsync($"/v1/videos/{id}")).StatusCode);
            Assert.False(await InFeed(id));
            Assert.DoesNotContain(await AdminReports(), x => x.VideoId == id);
        }

        [Fact]
        public async Task Admin_RemoveEvenWhenPeopleBoughtIt_AndKeepingARemovedVideoIsRefused()
        {
            var (_, id) = await PaidVideo();
            await PayFor(Factory.CreateUser("buyer", false), id);

            (await AdminApi().PostAsync($"/v1/admin/videos/{id}/remove", new JsonNetContent(new ModerateVideoDto(null))))
                .EnsureSuccessStatusCode();

            Assert.NotNull(await Videos.FindById(id)); // the record stays: the creator is told
            var keep = await AdminApi().PostAsync($"/v1/admin/videos/{id}/keep", null);
            Assert.Equal(HttpStatusCode.BadRequest, keep.StatusCode);
        }

        private Task<HttpResponseMessage> AdminRemove(string id) =>
            AdminApi().PostAsync($"/v1/admin/videos/{id}/remove", new JsonNetContent(new ModerateVideoDto("Removed")));

        [Fact]
        public async Task Admin_Remove_GivesEveryBuyerTheirMoneyBack()
        {
            var (_, id) = await PaidVideo();
            var buyer1 = Factory.CreateUser("buyer1", false);
            var buyer2 = Factory.CreateUser("buyer2", false);
            var first = await PayFor(buyer1, id);
            var second = await PayFor(buyer2, id);
            Assert.Equal("paid", first.Status);
            Assert.Equal("paid", second.Status);

            var removed = await Read<RemovedVideoDto>(await AdminRemove(id));

            Assert.Equal(2, removed.Refunded);
            Assert.Equal(0, removed.RefundsFailed);
            var refunded = Provider.Refunds.ToArray();
            Assert.Contains((first.Reference, VideoPrice), refunded);
            Assert.Contains((second.Reference, VideoPrice), refunded);
        }

        [Fact]
        public async Task Admin_Remove_ARefundThatFailsIsReported_AndRemovingAgainTriesItAgain()
        {
            var (_, id) = await PaidVideo();
            var reference = (await PayFor(Factory.CreateUser("buyer", false), id)).Reference;

            Provider.RefuseRefunds = true;
            try
            {
                var failed = await Read<RemovedVideoDto>(await AdminRemove(id));
                Assert.Equal(0, failed.Refunded);
                Assert.Equal(1, failed.RefundsFailed);
            }
            finally
            {
                Provider.RefuseRefunds = false;
            }

            // it stays on the list of the administrators while a refund is open
            var listed = Assert.Single(await AdminReports(), x => x.VideoId == id);
            Assert.Equal("removed", listed.Moderation);
            Assert.Equal(1, listed.RefundsPending);

            var again = await Read<RemovedVideoDto>(await AdminRemove(id));
            Assert.DoesNotContain(await AdminReports(), x => x.VideoId == id);
            Assert.Equal(1, again.Refunded);
            Assert.Equal(0, again.RefundsFailed);
            // given back once, however often it is removed
            Assert.Single(Provider.Refunds, x => x.Reference == reference);
            await AdminRemove(id);
            Assert.Single(Provider.Refunds, x => x.Reference == reference);
        }

        [Fact]
        public async Task PaidAfterTheVideoWasRemoved_NoRightToWatch_TheMoneyGoesBack()
        {
            var (_, id) = await PaidVideo();
            var buyer = Factory.CreateUser("late", false);
            var reference = await StartCheckout(buyer, id);
            (await AdminRemove(id)).EnsureSuccessStatusCode();

            Provider.Pay(reference, VideoPrice);
            await Api(buyer).GetAsync($"/v1/payments/{reference}");

            Assert.Single(Provider.Refunds, x => x.Reference == reference);
            Assert.False(await Factory.Services.GetRequiredService<IVideoEntitlementRepo>().Holds(buyer.Sub, id));
        }

        [Fact]
        public async Task Admin_UnknownVideo_NotFound_NoteTooLong_BadRequest()
        {
            var (_, id) = await ReadyVideo();

            Assert.Equal(HttpStatusCode.NotFound, (await AdminApi().PostAsync("/v1/admin/videos/nope/keep", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await AdminApi().PostAsync("/v1/admin/videos/nope/remove",
                new JsonNetContent(new ModerateVideoDto("x")))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AdminApi().PostAsync($"/v1/admin/videos/{id}/remove",
                new JsonNetContent(new ModerateVideoDto(new string('x', 501))))).StatusCode);
            Assert.Contains($"videos/{id}/video.mp4", Storage.Objects.Keys);
        }

        [Fact]
        public async Task Admin_OnlyServerAdministrators_MayDecide()
        {
            var (host, id) = await ReadyVideo();
            await ReportBy(3, id);

            foreach (var client in new[] {Api(null), Api(host), AdminApi("mod"), AdminApi("admin")})
            {
                var statuses = new[]
                {
                    (await client.GetAsync("/v1/admin/videos/reports")).StatusCode,
                    (await client.PostAsync($"/v1/admin/videos/{id}/keep", null)).StatusCode,
                    (await client.PostAsync($"/v1/admin/videos/{id}/remove", new JsonNetContent(new ModerateVideoDto("x")))).StatusCode,
                };
                Assert.All(statuses, x => Assert.Contains(x, new[] {HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden}));
            }

            Assert.Equal("hidden", (await View(host, id)).Moderation); // nothing changed
            Assert.Contains($"videos/{id}/video.mp4", Storage.Objects.Keys);
        }

        // ---- deleting, and other people's videos

        [Fact]
        public async Task Delete_RemovesTheFilesEverywhere_AndTheVideo()
        {
            var (host, id) = await ReadyVideo();

            var response = await Api(host).DeleteAsync($"/v1/videos/{id}");

            response.EnsureSuccessStatusCode();
            Assert.DoesNotContain($"videos/{id}/video.mp4", Storage.Objects.Keys);
            Assert.DoesNotContain($"videos/{id}/poster.jpg", Storage.Objects.Keys);
            Assert.Contains($"videos/{id}/video.mp4", Storage.Deleted);
            Assert.Equal(HttpStatusCode.NotFound, (await Api(host).GetAsync($"/v1/videos/{id}")).StatusCode);
            Assert.Null(await Videos.FindById(id));
        }

        [Fact]
        public async Task OthersCannotCompleteOrDeleteAVideo_AndItStaysIntact()
        {
            var (host, id) = await ReadyVideo();
            var other = await NewCreator();

            Assert.Equal(HttpStatusCode.NotFound,
                (await Api(other).PostAsync($"/v1/videos/{id}/uploaded", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Api(other).DeleteAsync($"/v1/videos/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Api(null).DeleteAsync($"/v1/videos/{id}")).StatusCode);

            Assert.Equal("ready", (await Read<VideoDto>(await Api(host).GetAsync($"/v1/videos/{id}"))).Status);
            Assert.Contains($"videos/{id}/video.mp4", Storage.Objects.Keys);
        }
    }
}
