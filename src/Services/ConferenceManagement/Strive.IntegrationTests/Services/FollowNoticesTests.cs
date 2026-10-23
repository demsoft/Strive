using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
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
    /// <summary>Mail to followers who asked for it: confirmed first, one at a time, stopped with one click.</summary>
    [Collection(IntegrationTestCollection.Definition)]
    public class FollowNoticesTests : ServiceIntegrationTest
    {
        public FollowNoticesTests(ITestOutputHelper testOutputHelper, MongoDbFixture mongoDb) : base(testOutputHelper,
            mongoDb)
        {
        }

        private FakeVideoConverter Converter => Factory.Services.GetRequiredService<FakeVideoConverter>();
        private FakeEmailSender Mail => Factory.Services.GetRequiredService<FakeEmailSender>();

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

        private async Task<(UserAccount host, string creatorId)> NewCreator()
        {
            var host = Factory.CreateUser("host" + Guid.NewGuid().ToString("N")[..6], false);
            var profile = await Read<CreatorProfileDto>(await Api(host).PutAsync("/v1/creators/me",
                new JsonNetContent(new SaveCreatorDto("Ada Lovelace", "Piano", "058", "0123456789"))));
            return (host, profile.CreatorId);
        }

        private UserAccount NewFan() => Factory.CreateUser("fan" + Guid.NewGuid().ToString("N")[..6], false);

        private Task<HttpResponseMessage> Follow(UserAccount fan, string creatorId) =>
            Api(fan).PutAsync($"/v1/creators/{creatorId}/follow", null);

        private Task<HttpResponseMessage> AskForMail(UserAccount? fan, string creatorId, string? email) =>
            Api(fan).PutAsync($"/v1/creators/{creatorId}/follow/email", new JsonNetContent(new StartNoticesDto(email)));

        private async Task<CreatorPageDto> Page(UserAccount? viewer, string creatorId) =>
            await Read<CreatorPageDto>(await Api(viewer).GetAsync($"/v1/creators/{creatorId}/page"));

        private async Task<string> ReadyVideo(UserAccount host, string title = "A new reel")
        {
            var created = await Read<CreatedVideoDto>(await Api(host).PostAsync("/v1/videos",
                new JsonNetContent(new CreateVideoDto(title, null, 50_000_000))));
            var id = created.Video.VideoId;
            Converter.Receive(id);
            (await Api(host).PostAsync($"/v1/videos/{id}/uploaded", null)).EnsureSuccessStatusCode();
            Converter.Finish(id);
            AssertSuccess(await Factory.Services.GetRequiredService<MediatR.IMediator>()
                .Send(new ProcessVideosRequest(id)));
            return id;
        }

        private async Task<int> RunNotices()
        {
            var result = await Factory.Services.GetRequiredService<MediatR.IMediator>()
                .Send(new SendFollowerNoticesRequest());
            AssertSuccess(result);
            return result.Response;
        }

        private IEnumerable<FakeEmailSender.Mail> MailsTo(string address) =>
            Mail.Sent.Where(x => x.To.Equals(address, StringComparison.OrdinalIgnoreCase));

        private static string TokenIn(FakeEmailSender.Mail mail, string kind) =>
            Regex.Match(mail.Text, $"/n/{kind}/([0-9a-f]+)").Groups[1].Value;

        /// <summary>A fan who follows, asked for mail and confirmed it</summary>
        private async Task<(UserAccount fan, string address, string stopToken)> Subscribed(string creatorId)
        {
            var fan = NewFan();
            var address = $"{Guid.NewGuid():N}@example.com";
            (await Follow(fan, creatorId)).EnsureSuccessStatusCode();
            (await AskForMail(fan, creatorId, address)).EnsureSuccessStatusCode();
            var token = TokenIn(MailsTo(address).Single(), "confirm");
            (await Api(null).PostAsync($"/v1/notices/{token}/confirm", null)).EnsureSuccessStatusCode();
            return (fan, address, token);
        }

        [Fact]
        public async Task NothingIsSentToAnAddress_UntilItsOwnerConfirmedIt()
        {
            var (host, creatorId) = await NewCreator();
            var fan = NewFan();
            var address = $"{Guid.NewGuid():N}@example.com";
            await Follow(fan, creatorId);

            (await AskForMail(fan, creatorId, "  " + address + " ")).EnsureSuccessStatusCode();

            var confirmation = Assert.Single(MailsTo(address));
            Assert.Contains("Ada Lovelace", confirmation.Subject);
            Assert.Equal("pending", (await Page(fan, creatorId)).Notice);

            // posts while the address is not confirmed: no mail
            await ReadyVideo(host);
            await RunNotices();
            Assert.Single(MailsTo(address));

            var token = TokenIn(confirmation, "confirm");
            var answer = await Read<NoticeAnswerDto>(await Api(null).PostAsync($"/v1/notices/{token}/confirm", null));
            Assert.Equal("Ada Lovelace", answer.CreatorName);
            Assert.Equal("on", answer.Status);
            Assert.Equal("on", (await Page(fan, creatorId)).Notice);
        }

        [Fact]
        public async Task ANewVideo_IsMailedToTheConfirmedFollowers_WithALinkToItAndALinkToStop()
        {
            var (host, creatorId) = await NewCreator();
            var (_, address, _) = await Subscribed(creatorId);

            var videoId = await ReadyVideo(host, "Piano in five minutes");
            Assert.True(await RunNotices() >= 1);

            var mail = MailsTo(address).Last();
            Assert.Contains("Piano in five minutes", mail.Subject);
            Assert.Contains("/v/" + videoId, mail.Text);
            Assert.Matches("/n/stop/[0-9a-f]+", mail.Text);
            Assert.Contains("/v/" + videoId, mail.Html);
        }

        [Fact]
        public async Task ANewSession_IsMailedToo()
        {
            var (host, creatorId) = await NewCreator();
            var (_, address, _) = await Subscribed(creatorId);

            var session = await Read<SessionDto>(await Api(host).PostAsync("/v1/sessions",
                new JsonNetContent(new CreateSessionDto("Jazz night", "Live", "music", 500000,
                    DateTimeOffset.UtcNow.AddDays(2)))));
            await RunNotices();

            var mail = MailsTo(address).Last();
            Assert.Contains("Jazz night", mail.Subject);
            Assert.Contains("/s/" + session.SessionId, mail.Text);
        }

        [Fact]
        public async Task OnlyTheFollowersOfThatCreator_AndOnlyOnce()
        {
            var (host, creatorId) = await NewCreator();
            var (otherHost, otherCreatorId) = await NewCreator();
            var (_, address, _) = await Subscribed(creatorId);
            var (_, otherAddress, _) = await Subscribed(otherCreatorId);

            await ReadyVideo(host);
            await RunNotices();
            await RunNotices();

            Assert.Equal(2, MailsTo(address).Count()); // the confirmation and the notice
            Assert.Single(MailsTo(otherAddress)); // only the confirmation
        }

        [Fact]
        public async Task SeveralPostsInARow_AreOneMail_BecauseOfTheCooldown()
        {
            var (host, creatorId) = await NewCreator();
            var (_, address, _) = await Subscribed(creatorId);

            await ReadyVideo(host, "First of three");
            await ReadyVideo(host, "Second of three");
            await ReadyVideo(host, "Third of three");
            await RunNotices();

            Assert.Equal(2, MailsTo(address).Count()); // the confirmation and one notice
            await ReadyVideo(host, "Fourth one");
            await RunNotices();
            Assert.Equal(2, MailsTo(address).Count());
        }

        [Fact]
        public async Task TheStopLink_StopsTheMails_AndCanBeClickedAgain_AndTheConfirmLinkIsDeadAfterwards()
        {
            var (host, creatorId) = await NewCreator();
            var (fan, address, confirmToken) = await Subscribed(creatorId);
            await ReadyVideo(host);
            await RunNotices();
            var stopToken = TokenIn(MailsTo(address).Last(), "stop");

            var stopped = await Read<NoticeAnswerDto>(await Api(null).PostAsync($"/v1/notices/{stopToken}/stop", null));
            var again = await Read<NoticeAnswerDto>(await Api(null).PostAsync($"/v1/notices/{stopToken}/stop", null));

            Assert.Equal("off", stopped.Status);
            Assert.Equal("off", again.Status);
            Assert.Equal("off", (await Page(fan, creatorId)).Notice);
            Assert.Equal(HttpStatusCode.NotFound,
                (await Api(null).PostAsync($"/v1/notices/{confirmToken}/confirm", null)).StatusCode);
            var before = MailsTo(address).Count();
            await ReadyVideo(host, "After stopping");
            await RunNotices();
            Assert.Equal(before, MailsTo(address).Count());
        }

        [Fact]
        public async Task TheFollower_CanStopFromThePageOfTheCreator_AndUnfollowingEndsTheMails()
        {
            var (host, creatorId) = await NewCreator();
            var (fan, address, _) = await Subscribed(creatorId);

            (await Api(fan).DeleteAsync($"/v1/creators/{creatorId}/follow/email")).EnsureSuccessStatusCode();
            Assert.Equal("off", (await Page(fan, creatorId)).Notice);
            // stopping twice is fine
            (await Api(fan).DeleteAsync($"/v1/creators/{creatorId}/follow/email")).EnsureSuccessStatusCode();

            var (fan2, address2, _) = await Subscribed(creatorId);
            (await Api(fan2).DeleteAsync($"/v1/creators/{creatorId}/follow")).EnsureSuccessStatusCode();
            await ReadyVideo(host);
            await RunNotices();

            Assert.Single(MailsTo(address));
            Assert.Single(MailsTo(address2));
        }

        [Fact]
        public async Task Asking_NeedsASignIn_AFollow_AndAnAddress()
        {
            var (_, creatorId) = await NewCreator();
            var fan = NewFan();

            Assert.Equal(HttpStatusCode.Unauthorized, (await AskForMail(null, creatorId, "a@example.com")).StatusCode);
            var notFollowing = await AskForMail(fan, creatorId, "a@example.com");
            Assert.Equal(HttpStatusCode.BadRequest, notFollowing.StatusCode);
            Assert.Contains("Social_NotFollowing", await notFollowing.Content.ReadAsStringAsync());

            await Follow(fan, creatorId);
            foreach (var bad in new[] {"", "nope", "a@b", null})
            {
                var response = await AskForMail(fan, creatorId, bad);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Contains("Social_InvalidEmail", await response.Content.ReadAsStringAsync());
            }

            Assert.Empty(Mail.Sent);
            Assert.Equal("off", (await Page(fan, creatorId)).Notice);
        }

        [Fact]
        public async Task WhenMailCannotBeSent_ItIsSaid_AndNothingIsSignedUp()
        {
            var (_, creatorId) = await NewCreator();
            var fan = NewFan();
            await Follow(fan, creatorId);

            Mail.Failing = true;
            var failed = await AskForMail(fan, creatorId, "a@example.com");
            Assert.Equal(HttpStatusCode.BadRequest, failed.StatusCode);
            Assert.Contains("Social_EmailFailed", await failed.Content.ReadAsStringAsync());
            Assert.Equal("off", (await Page(fan, creatorId)).Notice);

            Mail.Failing = false;
            Mail.Configured = false;
            var notSetUp = await AskForMail(fan, creatorId, "a@example.com");
            Assert.Contains("Social_EmailNotAvailable", await notSetUp.Content.ReadAsStringAsync());
            Assert.False((await Page(fan, creatorId)).NoticesAvailable);
        }

        [Fact]
        public async Task NothingIsUsedUp_WhileMailCannotBeSent()
        {
            var (host, creatorId) = await NewCreator();
            var (_, address, _) = await Subscribed(creatorId);
            Mail.Configured = false;
            await ReadyVideo(host);

            Assert.Equal(0, await RunNotices());

            Mail.Configured = true;
            await RunNotices();
            Assert.Equal(2, MailsTo(address).Count());
        }

        [Fact]
        public async Task UnknownOrEmptyLinks_NotFound()
        {
            Assert.Equal(HttpStatusCode.NotFound,
                (await Api(null).PostAsync("/v1/notices/deadbeef/confirm", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await Api(null).PostAsync("/v1/notices/deadbeef/stop", null)).StatusCode);
        }

        [Fact]
        public async Task ThePage_SaysWhetherMailIsAvailable_AndWhatTheViewerChose()
        {
            var (_, creatorId) = await NewCreator();

            var visitor = await Page(null, creatorId);

            Assert.True(visitor.NoticesAvailable);
            Assert.Equal("off", visitor.Notice);
        }
    }
}
