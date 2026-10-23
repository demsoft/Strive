using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Strive.Core.Services.Payments;
using Strive.Core.Services.Payments.Gateways;
using Strive.Core.Services.Payments.Requests;
using Strive.Core.Services.Payments.Sessions;
using Strive.Hubs.Core;
using Strive.Infrastructure.Serialization;
using Strive.IntegrationTests._Helpers;
using Strive.Models.Response;
using Xunit;
using Xunit.Abstractions;

namespace Strive.IntegrationTests.Services
{
    /// <summary>
    ///     Paid sessions from start to end: the creator publishes, a fan pays (the provider is a fake), the ticket is
    ///     granted once, and only then the room opens.
    /// </summary>
    [Collection(IntegrationTestCollection.Definition)]
    public class SessionsTests : ServiceIntegrationTest
    {
        private const long Price = 500_000; // ₦5,000

        public SessionsTests(ITestOutputHelper testOutputHelper, MongoDbFixture mongoDb) : base(testOutputHelper,
            mongoDb)
        {
        }

        private FakePaymentProvider Provider => Factory.Services.GetRequiredService<FakePaymentProvider>();

        private UserAccount NewUser(string prefix) => Factory.CreateUser(prefix + Guid.NewGuid().ToString("N")[..6], false);

        private HttpClient Client(UserAccount? user)
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
            var host = NewUser("host");
            (await Client(host).PutAsync("/v1/creators/me",
                new JsonNetContent(new SaveCreatorDto("Ada Lovelace", "Piano", "058", "0123456789"))))
                .EnsureSuccessStatusCode();
            return host;
        }

        private async Task<SessionDto> Publish(UserAccount host, long price = Price, string title = "Piano for beginners",
            string category = "music")
        {
            return await Read<SessionDto>(await Client(host).PostAsync("/v1/sessions",
                new JsonNetContent(new CreateSessionDto(title, "Learn the basics", category, price, null))));
        }

        private async Task<(UserAccount host, SessionDto session)> PublishedSession(long price = Price)
        {
            var host = await NewCreator();
            return (host, await Publish(host, price));
        }

        private async Task<CheckoutResultDto> Checkout(UserAccount fan, string sessionId, string email = "fan@example.com")
        {
            return await Read<CheckoutResultDto>(await Client(fan).PostAsync($"/v1/sessions/{sessionId}/checkout",
                new JsonNetContent(new CheckoutDto(email))));
        }

        private async Task<PaymentStatusDto> Status(UserAccount fan, string reference)
        {
            return await Read<PaymentStatusDto>(await Client(fan).GetAsync($"/v1/payments/{reference}"));
        }

        private async Task<UserAccount> FanWithTicket(SessionDto session)
        {
            var fan = NewUser("fan");
            var checkout = await Checkout(fan, session.SessionId);
            Provider.Pay(checkout.Reference!, session.PriceMinor);
            await Status(fan, checkout.Reference!);
            return fan;
        }

        private async Task<HttpResponseMessage> Webhook(string json, string? signature = "valid")
        {
            var body = Encoding.UTF8.GetBytes(json);
            var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/paystack")
            {
                Content = new ByteArrayContent(body),
            };
            request.Headers.Add("x-paystack-signature", signature == "valid" ? FakePaymentProvider.Sign(body) : signature);
            return await Factory.CreateClient().SendAsync(request);
        }

        // ---- publish

        // ---- refunds

        private Task<HttpResponseMessage> Cancel(UserAccount host, string sessionId) =>
            Client(host).PostAsync($"/v1/sessions/{sessionId}/cancel", null);

        private async Task<TicketDto> TicketOf(UserAccount fan, string sessionId) =>
            Assert.Single(await Read<TicketDto[]>(await Client(fan).GetAsync("/v1/tickets/mine")),
                x => x.SessionId == sessionId);

        [Fact]
        public async Task Cancel_PaidTickets_AreRefundedInFull()
        {
            // arrange
            var (host, session) = await PublishedSession();
            var fan = await FanWithTicket(session);
            var reference = Provider.Checkouts.Last().Reference;

            // act
            (await Cancel(host, session.SessionId)).EnsureSuccessStatusCode();

            // assert: the provider gave all of it back, the fan sees it, the host sees the number
            Assert.Contains(Provider.Refunds, x => x.Reference == reference && x.AmountMinor == session.PriceMinor);
            Assert.True((await TicketOf(fan, session.SessionId)).Refunded);
            var mine = await Read<SessionDto[]>(await Client(host).GetAsync("/v1/sessions/mine"));
            var entry = Assert.Single(mine, x => x.SessionId == session.SessionId);
            Assert.Equal(1, entry.Refunded);
            Assert.Equal(0, entry.RefundsPending);
        }

        [Fact]
        public async Task Cancel_ProviderRefusesRefund_StillCancelsAndRetryRefunds()
        {
            // arrange
            var (host, session) = await PublishedSession();
            var fan = await FanWithTicket(session);
            Provider.RefuseRefunds = true;

            // act: the cancellation holds, the refund is open
            (await Cancel(host, session.SessionId)).EnsureSuccessStatusCode();
            var open = Assert.Single(await Read<SessionDto[]>(await Client(host).GetAsync("/v1/sessions/mine")),
                x => x.SessionId == session.SessionId);

            // assert
            Assert.Equal(SessionStatus.Cancelled, open.Status);
            Assert.Equal(1, open.RefundsPending);
            Assert.False((await TicketOf(fan, session.SessionId)).Refunded);

            // act: the provider works again and the host tries once more
            Provider.RefuseRefunds = false;
            (await Cancel(host, session.SessionId)).EnsureSuccessStatusCode();

            // assert
            Assert.True((await TicketOf(fan, session.SessionId)).Refunded);
        }

        [Fact]
        public async Task Cancel_Twice_RefundsOnlyOnce()
        {
            var (host, session) = await PublishedSession();
            await FanWithTicket(session);
            var reference = Provider.Checkouts.Last().Reference;

            await Cancel(host, session.SessionId);
            await Cancel(host, session.SessionId);

            Assert.Single(Provider.Refunds, x => x.Reference == reference);
        }

        [Fact]
        public async Task Cancel_ByAnotherPerson_RefundsNothing()
        {
            var (_, session) = await PublishedSession();
            await FanWithTicket(session);
            var before = Provider.Refunds.Count;

            var response = await Cancel(NewUser("other"), session.SessionId);

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(before, Provider.Refunds.Count);
        }

        [Fact]
        public async Task Payment_ConfirmedAfterCancel_IsRefundedAndGivesNoTicket()
        {
            // arrange: the fan started to pay, then the host called the session off
            var (host, session) = await PublishedSession();
            var fan = NewUser("fan");
            var checkout = await Checkout(fan, session.SessionId);
            await Cancel(host, session.SessionId);

            // act: the money arrives after that
            Provider.Pay(checkout.Reference!, session.PriceMinor);
            var status = await Status(fan, checkout.Reference!);

            // assert
            Assert.Equal("refunded", status.Status);
            Assert.False(status.HasTicket);
            Assert.Contains(Provider.Refunds, x => x.Reference == checkout.Reference);
        }

        // ---- the page of a creator

        private async Task<string> CreatorIdOf(UserAccount host) =>
            (await Read<CreatorProfileDto>(await Client(host).GetAsync("/v1/creators/me"))).CreatorId;

        [Fact]
        public async Task CreatorPage_ShowsTheCreatorAndOnlyTheirOpenSessions_ForEverybody()
        {
            // arrange: two creators, one of them has a session open and one called off
            var host = await NewCreator();
            var open = await Publish(host, title: "Open for tickets");
            var called = await Publish(host, title: "Called off");
            await Cancel(host, called.SessionId);
            var other = await NewCreator();
            var othersSession = await Publish(other, title: "Someone else");

            // act: nobody is signed in
            var page = await Read<CreatorPageDto>(
                await Client(null).GetAsync($"/v1/creators/{await CreatorIdOf(host)}/page"));

            // assert
            Assert.Equal("Ada Lovelace", page.Creator.DisplayName);
            Assert.Equal("Piano", page.Creator.Bio);
            Assert.Equal(open.SessionId, Assert.Single(page.Sessions).SessionId);
            Assert.DoesNotContain(page.Sessions, x => x.SessionId == othersSession.SessionId);
        }

        [Fact]
        public async Task CreatorPage_NoBankDetailsAreShown()
        {
            var host = await NewCreator();

            var response = await Client(null).GetAsync($"/v1/creators/{await CreatorIdOf(host)}/page");
            var json = await response.Content.ReadAsStringAsync();

            Assert.DoesNotContain("0123456789", json);
            Assert.DoesNotContain("ccountLast4", json);
            Assert.DoesNotContain("ubaccount", json);
        }

        [Fact]
        public async Task CreatorPage_UnknownCreator_NotFound()
        {
            var response = await Client(null).GetAsync("/v1/creators/does-not-exist/page");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // ---- passes

        private const long PassPrice = 2_000_000; // ₦20,000

        private Task<HttpResponseMessage> SetPassPrice(UserAccount host, long? price) =>
            Client(host).PutAsync("/v1/creators/me/pass", new JsonNetContent(new SetPassPriceDto(price)));

        private async Task<(UserAccount host, string creatorId)> CreatorWithPass()
        {
            var host = await NewCreator();
            (await SetPassPrice(host, PassPrice)).EnsureSuccessStatusCode();
            return (host, await CreatorIdOf(host));
        }

        private async Task<CheckoutResultDto> PassCheckout(UserAccount fan, string creatorId) =>
            await Read<CheckoutResultDto>(await Client(fan).PostAsync($"/v1/creators/{creatorId}/pass/checkout",
                new JsonNetContent(new CheckoutDto("fan@example.com"))));

        private async Task<UserAccount> FanWithPass(string creatorId, UserAccount? existing = null)
        {
            var fan = existing ?? NewUser("fan");
            var checkout = await PassCheckout(fan, creatorId);
            Provider.Pay(checkout.Reference!, PassPrice);
            var status = await Status(fan, checkout.Reference!);
            Assert.Equal("paid", status.Status);
            Assert.Equal("pass", status.Kind);
            return fan;
        }

        private async Task<CreatorPageDto> PageOf(string creatorId, UserAccount? viewer) =>
            await Read<CreatorPageDto>(await Client(viewer).GetAsync($"/v1/creators/{creatorId}/page"));

        [Fact]
        public async Task Pass_CreatorSetsAPrice_ItIsShownOnTheirPage()
        {
            var (_, creatorId) = await CreatorWithPass();

            var page = await PageOf(creatorId, null);

            Assert.Equal(PassPrice, page.Creator.PassPriceMinor);
            Assert.False(page.HasPass);
        }

        [Theory]
        [InlineData(100L)]
        [InlineData(1_000_000_000L)]
        public async Task Pass_PriceOutOfRange_BadRequest(long price)
        {
            var host = await NewCreator();

            Assert.Equal(HttpStatusCode.BadRequest, (await SetPassPrice(host, price)).StatusCode);
        }

        [Fact]
        public async Task Pass_NullPrice_StopsSellingIt()
        {
            var (host, creatorId) = await CreatorWithPass();

            (await SetPassPrice(host, null)).EnsureSuccessStatusCode();

            Assert.Null((await PageOf(creatorId, null)).Creator.PassPriceMinor);
            Assert.Equal(HttpStatusCode.BadRequest, (await Client(NewUser("fan")).PostAsync(
                $"/v1/creators/{creatorId}/pass/checkout",
                new JsonNetContent(new CheckoutDto("fan@example.com")))).StatusCode);
        }

        [Fact]
        public async Task Pass_NotACreator_CannotSetAPrice()
        {
            var response = await SetPassPrice(NewUser("fan"), PassPrice);

            Assert.False(response.IsSuccessStatusCode);
        }

        [Fact]
        public async Task Pass_Checkout_UsesThePassPriceAndTheCreatorsShare()
        {
            var (_, creatorId) = await CreatorWithPass();

            await PassCheckout(NewUser("fan"), creatorId);

            var request = Provider.Checkouts.Last();
            Assert.Equal(PassPrice, request.AmountMinor);
            Assert.Equal(PassPrice / 10, request.PlatformAmountMinor); // 10% commission
        }

        [Fact]
        public async Task Pass_Paid_OpensEverySessionOfThatCreatorOnly()
        {
            // arrange
            var (host, creatorId) = await CreatorWithPass();
            var first = await Publish(host, title: "First");
            var second = await Publish(host, title: "Second");
            var (_, other) = await PublishedSession();

            // act
            var fan = await FanWithPass(creatorId);

            // assert: both sessions of the creator are open to the fan, the other creator's is not
            foreach (var session in new[] {first, second})
                Assert.True(
                    (await Read<PublicSessionDto>(await Client(fan).GetAsync($"/v1/sessions/{session.SessionId}")))
                    .HasTicket);
            Assert.False(
                (await Read<PublicSessionDto>(await Client(fan).GetAsync($"/v1/sessions/{other.SessionId}")))
                .HasTicket);
            Assert.True((await PageOf(creatorId, fan)).HasPass);
        }

        [Fact]
        public async Task Pass_Paid_FanCanJoinTheRoom()
        {
            var (host, creatorId) = await CreatorWithPass();
            var session = await Publish(host);
            var hostConnection = await ConnectUserToConference(host, Room(session));
            AssertSuccess(await OpenConference(hostConnection));
            var fan = await FanWithPass(creatorId);

            var connection = await ConnectUserToConference(fan, Room(session));

            Assert.NotNull(connection);
        }

        [Fact]
        public async Task Pass_Paid_TicketCheckoutOfThatCreatorIsNotNeeded()
        {
            var (host, creatorId) = await CreatorWithPass();
            var session = await Publish(host);
            var fan = await FanWithPass(creatorId);

            var checkout = await Checkout(fan, session.SessionId);

            Assert.True(checkout.AlreadyHasTicket);
        }

        [Fact]
        public async Task Pass_NotPaid_GivesNothing()
        {
            var (host, creatorId) = await CreatorWithPass();
            var session = await Publish(host);
            var fan = NewUser("fan");
            await PassCheckout(fan, creatorId);

            Assert.False(
                (await Read<PublicSessionDto>(await Client(fan).GetAsync($"/v1/sessions/{session.SessionId}")))
                .HasTicket);
            Assert.False((await PageOf(creatorId, fan)).HasPass);
        }

        [Fact]
        public async Task Pass_BoughtAgainWhileRunning_AddsTheTime()
        {
            var (_, creatorId) = await CreatorWithPass();
            var fan = await FanWithPass(creatorId);
            var first = (await PageOf(creatorId, fan)).PassValidUntil!.Value;

            await FanWithPass(creatorId, fan);
            var second = (await PageOf(creatorId, fan)).PassValidUntil!.Value;

            Assert.InRange((second - first).TotalDays, 29.9, 30.1);
        }

        [Fact]
        public async Task Pass_TheCreatorCannotBuyTheirOwn()
        {
            var (host, creatorId) = await CreatorWithPass();

            var response = await Client(host).PostAsync($"/v1/creators/{creatorId}/pass/checkout",
                new JsonNetContent(new CheckoutDto("host@example.com")));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Pass_MyPasses_ListsTheRunningOnes()
        {
            var (_, creatorId) = await CreatorWithPass();
            var fan = await FanWithPass(creatorId);

            var passes = await Read<PassDto[]>(await Client(fan).GetAsync("/v1/passes/mine"));

            var pass = Assert.Single(passes);
            Assert.Equal(creatorId, pass.CreatorId);
            Assert.Equal("Ada Lovelace", pass.CreatorName);
        }

        [Fact]
        public async Task Pass_SessionCalledOff_PassIsNotRefundedAndStillWorks()
        {
            // arrange
            var (host, creatorId) = await CreatorWithPass();
            var cancelled = await Publish(host, title: "Cancelled");
            var next = await Publish(host, title: "Next");
            var fan = await FanWithPass(creatorId);
            var passReference = Provider.Checkouts.Last().Reference;

            // act
            await Cancel(host, cancelled.SessionId);

            // assert: the pass money stays, the other session is still open to the fan
            Assert.DoesNotContain(Provider.Refunds, x => x.Reference == passReference);
            Assert.True(
                (await Read<PublicSessionDto>(await Client(fan).GetAsync($"/v1/sessions/{next.SessionId}")))
                .HasTicket);
        }

        // ---- earnings

        private async Task<EarningsDto> EarningsOf(UserAccount host) =>
            await Read<EarningsDto>(await Client(host).GetAsync("/v1/creators/me/earnings"));

        [Fact]
        public async Task Earnings_ShowTicketSalesWithCommissionTakenOff()
        {
            // arrange: two fans buy one session
            var (host, session) = await PublishedSession();
            await FanWithTicket(session);
            await FanWithTicket(session);

            // act
            var earnings = await EarningsOf(host);

            // assert: 10% commission of ₦10,000
            Assert.Equal("NGN", earnings.Currency);
            Assert.Equal(2 * Price, earnings.Totals.GrossMinor);
            Assert.Equal(2 * Price / 10, earnings.Totals.PlatformMinor);
            Assert.Equal(2 * Price * 9 / 10, earnings.Totals.NetMinor);
            Assert.Equal(2, earnings.Totals.Tickets);
            var row = Assert.Single(earnings.Sessions);
            Assert.Equal(session.Title, row.Title);
            Assert.Equal(2, row.Tickets);
            Assert.Equal(2, earnings.Recent.Count);
        }

        [Fact]
        public async Task Earnings_PassSalesAreIncluded()
        {
            var (host, creatorId) = await CreatorWithPass();
            await FanWithPass(creatorId);

            var earnings = await EarningsOf(host);

            Assert.Equal(1, earnings.Totals.Passes);
            Assert.Equal(PassPrice * 9 / 10, earnings.Totals.NetMinor);
            Assert.Equal("pass", Assert.Single(earnings.Recent).Kind);
        }

        [Fact]
        public async Task Earnings_RefundedTicketsLeaveTheTotals()
        {
            var (host, session) = await PublishedSession();
            await FanWithTicket(session);
            await Cancel(host, session.SessionId);

            var earnings = await EarningsOf(host);

            Assert.Equal(0, earnings.Totals.NetMinor);
            Assert.Equal(Price, earnings.Totals.RefundedMinor);
            Assert.True(Assert.Single(earnings.Recent).Refunded);
        }

        [Fact]
        public async Task Earnings_OnlyContainTheSalesOfThatCreator()
        {
            var (host, session) = await PublishedSession();
            await FanWithTicket(session);
            var (other, otherSession) = await PublishedSession();

            var earnings = await EarningsOf(other);

            Assert.Equal(0, earnings.Totals.GrossMinor);
            Assert.Empty(earnings.Recent);
            Assert.Equal(1, (await EarningsOf(host)).Totals.Tickets);
        }

        [Fact]
        public async Task Earnings_BuyerIsMasked()
        {
            var (host, session) = await PublishedSession();
            await FanWithTicket(session);

            var earnings = await EarningsOf(host);

            Assert.Equal("f**@example.com", Assert.Single(earnings.Recent).Buyer);
        }

        [Fact]
        public async Task Earnings_NotACreator_NotFound_AndNotSignedIn_Unauthorized()
        {
            Assert.Equal(HttpStatusCode.NotFound,
                (await Client(NewUser("fan")).GetAsync("/v1/creators/me/earnings")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await Client(null).GetAsync("/v1/creators/me/earnings")).StatusCode);
        }

        // ---- reminders

        private FakeEmailSender Mail => Factory.Services.GetRequiredService<FakeEmailSender>();

        private async Task<int> RunReminders()
        {
            var result = await Factory.Services.GetRequiredService<MediatR.IMediator>()
                .Send(new Strive.Core.Services.Reminders.SendSessionRemindersRequest());
            AssertSuccess(result);
            return result.Response;
        }

        private async Task<SessionDto> PublishAt(UserAccount host, DateTimeOffset? startsAt, string title = "Starting soon")
        {
            return await Read<SessionDto>(await Client(host).PostAsync("/v1/sessions",
                new JsonNetContent(new CreateSessionDto(title, "Learn", "music", Price, startsAt))));
        }

        private async Task BuyTicket(SessionDto session, string email)
        {
            var fan = NewUser("fan");
            var checkout = await Checkout(fan, session.SessionId, email);
            Provider.Pay(checkout.Reference!, session.PriceMinor);
            await Status(fan, checkout.Reference!);
        }

        private IEnumerable<FakeEmailSender.Mail> MailsAbout(SessionDto session) =>
            Mail.Sent.Where(x => x.Text.Contains("/s/" + session.SessionId));

        [Fact]
        public async Task Reminder_SessionStartsSoon_EveryTicketHolderGetsOneMail()
        {
            // arrange: two fans, one of them with two tickets (the same address is mailed once)
            var host = await NewCreator();
            var session = await PublishAt(host, DateTimeOffset.UtcNow.AddMinutes(30));
            await BuyTicket(session, "ada@example.com");
            await BuyTicket(session, "kofi@example.com");
            await BuyTicket(session, "ADA@example.com");

            // act
            await RunReminders();

            // assert
            var mails = MailsAbout(session).ToList();
            Assert.Equal(new[] {"ada@example.com", "kofi@example.com"},
                mails.Select(x => x.To.ToLowerInvariant()).OrderBy(x => x));
            Assert.All(mails, mail =>
            {
                Assert.Contains("Starting soon", mail.Subject);
                Assert.Contains("Ada Lovelace", mail.Text);
                Assert.Contains("WAT", mail.Text);
            });
        }

        [Fact]
        public async Task Reminder_IsSentOnlyOnce()
        {
            var host = await NewCreator();
            var session = await PublishAt(host, DateTimeOffset.UtcNow.AddMinutes(30));
            await BuyTicket(session, "ada@example.com");

            await RunReminders();
            await RunReminders();

            Assert.Single(MailsAbout(session));
        }

        [Fact]
        public async Task Reminder_TooEarlyStartedCancelledOrUndated_NoMail()
        {
            // arrange
            var host = await NewCreator();
            var later = await PublishAt(host, DateTimeOffset.UtcNow.AddHours(5));
            var started = await PublishAt(host, DateTimeOffset.UtcNow.AddMinutes(-5));
            var undated = await PublishAt(host, null);
            var cancelled = await PublishAt(host, DateTimeOffset.UtcNow.AddMinutes(30));
            foreach (var session in new[] {later, started, undated, cancelled}) await BuyTicket(session, "ada@example.com");
            await Cancel(host, cancelled.SessionId);
            // act
            await RunReminders();

            // assert
            foreach (var session in new[] {later, started, undated, cancelled}) Assert.Empty(MailsAbout(session));
        }

        [Fact]
        public async Task Reminder_PendingAndRefundedOrders_GetNoMail()
        {
            var host = await NewCreator();
            var session = await PublishAt(host, DateTimeOffset.UtcNow.AddMinutes(30));
            await Checkout(NewUser("fan"), session.SessionId, "unpaid@example.com"); // never paid

            await RunReminders();

            Assert.Empty(MailsAbout(session));
        }

        [Fact]
        public async Task Reminder_MailsNotSetUp_NothingIsUsedUp_ItGoesOutLater()
        {
            var host = await NewCreator();
            var session = await PublishAt(host, DateTimeOffset.UtcNow.AddMinutes(30));
            await BuyTicket(session, "ada@example.com");
            Mail.Configured = false;
            try
            {
                await RunReminders();
                Assert.Empty(MailsAbout(session));
            }
            finally
            {
                Mail.Configured = true;
            }

            await RunReminders();

            Assert.Single(MailsAbout(session));
        }

        [Fact]
        public async Task Reminder_MailServiceDown_IsTriedAgainAtTheNextRound()
        {
            var host = await NewCreator();
            var session = await PublishAt(host, DateTimeOffset.UtcNow.AddMinutes(30));
            await BuyTicket(session, "ada@example.com");
            Mail.Failing = true;
            try
            {
                Assert.Equal(0, await RunReminders());
            }
            finally
            {
                Mail.Failing = false;
            }

            await RunReminders();

            Assert.Single(MailsAbout(session));
        }

        // ---- the name in the link of a creator page

        private Task<HttpResponseMessage> SetSlug(UserAccount host, string? slug) =>
            Client(host).PutAsync("/v1/creators/me/slug", new JsonNetContent(new SetSlugDto(slug)));

        [Fact]
        public async Task Slug_PageIsFoundByTheNameAndById()
        {
            var host = await NewCreator();
            var creatorId = await CreatorIdOf(host);
            var name = "ada" + Guid.NewGuid().ToString("N")[..8];

            (await SetSlug(host, name)).EnsureSuccessStatusCode();

            var byName = await PageOf(name, null);
            var byId = await PageOf(creatorId, null);
            Assert.Equal(creatorId, byName.Creator.CreatorId);
            Assert.Equal(name, byName.Creator.Slug);
            Assert.Equal(creatorId, byId.Creator.CreatorId);
        }

        [Fact]
        public async Task Slug_CapitalsAndSpacesAreForgiven()
        {
            var host = await NewCreator();
            var name = "ada" + Guid.NewGuid().ToString("N")[..8];

            var response = await SetSlug(host, "  " + name.ToUpperInvariant() + " ");

            response.EnsureSuccessStatusCode();
            Assert.Equal(name, (await Read<CreatorProfileDto>(response)).Slug);
            Assert.Equal(await CreatorIdOf(host), (await PageOf(name.ToUpperInvariant(), null)).Creator.CreatorId);
        }

        [Fact]
        public async Task Slug_TakenByAnotherCreator_BadRequest_AndTheOwnerKeepsIt()
        {
            var first = await NewCreator();
            var second = await NewCreator();
            var name = "ada" + Guid.NewGuid().ToString("N")[..8];
            (await SetSlug(first, name)).EnsureSuccessStatusCode();

            var response = await SetSlug(second, name);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("Payments_SlugTaken", await response.Content.ReadAsStringAsync());
            Assert.Equal(await CreatorIdOf(first), (await PageOf(name, null)).Creator.CreatorId);
        }

        [Fact]
        public async Task Slug_SettingYourOwnAgain_IsFine()
        {
            var host = await NewCreator();
            var name = "ada" + Guid.NewGuid().ToString("N")[..8];
            (await SetSlug(host, name)).EnsureSuccessStatusCode();

            (await SetSlug(host, name)).EnsureSuccessStatusCode();
        }

        [Theory]
        [InlineData("ab")]
        [InlineData("has space")]
        [InlineData("under_score")]
        [InlineData("admin")]
        public async Task Slug_Invalid_BadRequestWithAReason(string slug)
        {
            var response = await SetSlug(await NewCreator(), slug);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("Payments_InvalidSlug", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Slug_Cleared_GoesBackToTheId_AndTheNameIsFreeAgain()
        {
            var host = await NewCreator();
            var other = await NewCreator();
            var name = "ada" + Guid.NewGuid().ToString("N")[..8];
            (await SetSlug(host, name)).EnsureSuccessStatusCode();

            (await SetSlug(host, null)).EnsureSuccessStatusCode();

            Assert.Equal(HttpStatusCode.NotFound,
                (await Client(null).GetAsync($"/v1/creators/{name}/page")).StatusCode);
            (await SetSlug(other, name)).EnsureSuccessStatusCode();
        }

        [Fact]
        public async Task Slug_TwoCreatorsWithoutOne_DoNotCollide()
        {
            // the unique index only counts names that are set
            var host = await NewCreator();
            await NewCreator();
            await NewCreator();

            Assert.Null((await Read<CreatorProfileDto>(await Client(host).GetAsync("/v1/creators/me"))).Slug);
        }

        [Fact]
        public async Task Slug_NotACreator_NotFound()
        {
            var response = await SetSlug(NewUser("fan"), "somebody");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // ---- passes that renew themselves

        private IPassSubscriptionRepo Subscriptions => Factory.Services.GetRequiredService<IPassSubscriptionRepo>();

        private async Task<(UserAccount fan, PassSubscription subscription)> RenewingFan(string creatorId,
            string token = "AUTH_card1", UserAccount? existing = null)
        {
            var fan = existing ?? NewUser("fan");
            var response = await Client(fan).PostAsync($"/v1/creators/{creatorId}/pass/checkout",
                new JsonNetContent(new CheckoutDto("fan@example.com", true)));
            var checkout = await Read<CheckoutResultDto>(response);
            Provider.Pay(checkout.Reference!, PassPrice, cardToken: token);
            Assert.Equal("paid", (await Status(fan, checkout.Reference!)).Status);
            var subscription = (await Subscriptions.FindActiveOfUser(fan.Sub)).Single(x => x.CreatorId == creatorId);
            return (fan, subscription);
        }

        private async Task<RenewalSummary> RunRenewals()
        {
            var result = await Factory.Services.GetRequiredService<MediatR.IMediator>()
                .Send(new Strive.Core.Services.Payments.Requests.RenewPassSubscriptionsRequest());
            AssertSuccess(result);
            return result.Response!;
        }

        private async Task MakeDue(PassSubscription subscription)
        {
            var fresh = (await Subscriptions.FindById(subscription.SubscriptionId))!;
            fresh.NextChargeAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await Subscriptions.Update(fresh);
        }

        private Task<PassSubscription> Reload(PassSubscription subscription) =>
            Subscriptions.FindById(subscription.SubscriptionId)!;

        private IEnumerable<ChargeAuthorizationRequest> ChargesOf(PassSubscription subscription) =>
            Provider.Charges.Where(x => x.Metadata.GetValueOrDefault("subscriptionId") == subscription.SubscriptionId);

        [Fact]
        public async Task Renew_PaidWithARenewingCard_StartsASubscriptionADayBeforeThePassEnds()
        {
            var (_, creatorId) = await CreatorWithPass();

            var (fan, subscription) = await RenewingFan(creatorId);

            var until = (await PageOf(creatorId, fan)).PassValidUntil!.Value;
            Assert.Equal("AUTH_card1", subscription.AuthorizationCode);
            Assert.Equal(PassPrice, subscription.AmountMinor);
            Assert.InRange((subscription.NextChargeAt - (until - TimeSpan.FromDays(1))).TotalSeconds, -5, 5);
        }

        [Fact]
        public async Task Renew_NotAgreedOrCardCannotBeChargedAgain_NoSubscription()
        {
            var (_, creatorId) = await CreatorWithPass();

            // not ticked
            var fan = await FanWithPass(creatorId);
            Assert.Empty(await Subscriptions.FindActiveOfUser(fan.Sub));

            // ticked, paid by bank transfer (no token)
            var other = NewUser("fan");
            var checkout = await Read<CheckoutResultDto>(await Client(other).PostAsync(
                $"/v1/creators/{creatorId}/pass/checkout", new JsonNetContent(new CheckoutDto("fan@example.com", true))));
            Provider.Pay(checkout.Reference!, PassPrice);
            await Status(other, checkout.Reference!);

            Assert.Empty(await Subscriptions.FindActiveOfUser(other.Sub));
            Assert.True((await PageOf(creatorId, other)).HasPass);
        }

        [Fact]
        public async Task Renew_TheCardTokenIsNeverSentToABrowser()
        {
            var (_, creatorId) = await CreatorWithPass();
            var (fan, _) = await RenewingFan(creatorId, "AUTH_secret_token");

            var bodies = new[]
            {
                await Client(fan).GetStringAsync("/v1/passes/mine"),
                await Client(fan).GetStringAsync($"/v1/creators/{creatorId}/page"),
            };

            Assert.All(bodies, body => Assert.DoesNotContain("AUTH_secret_token", body));
            Assert.All(bodies, body => Assert.DoesNotContain("uthorizationCode", body));
        }

        [Fact]
        public async Task Renew_Due_ChargesTheCardWithTheSameSplit_AndAddsThirtyDays()
        {
            var (host, creatorId) = await CreatorWithPass();
            var (fan, subscription) = await RenewingFan(creatorId);
            var before = (await PageOf(creatorId, fan)).PassValidUntil!.Value;
            await MakeDue(subscription);

            await RunRenewals();

            var charge = Assert.Single(ChargesOf(subscription));
            Assert.Equal("AUTH_card1", charge.AuthorizationCode);
            Assert.Equal(PassPrice, charge.AmountMinor);
            Assert.Equal(PassPrice / 10, charge.PlatformAmountMinor);
            var page = await PageOf(creatorId, fan);
            Assert.InRange((page.PassValidUntil!.Value - before).TotalDays, 29.9, 30.1);
            var after = await Reload(subscription);
            Assert.Equal(0, after.FailedAttempts);
            Assert.Null(after.PendingReference);
            Assert.InRange((after.NextChargeAt - (page.PassValidUntil.Value - TimeSpan.FromDays(1))).TotalSeconds, -5, 5);
            // the creator earned from both payments
            Assert.Equal(2, (await EarningsOf(host)).Totals.Passes);
        }

        [Fact]
        public async Task Renew_NotDueYet_NothingIsCharged()
        {
            var (_, creatorId) = await CreatorWithPass();
            var (_, subscription) = await RenewingFan(creatorId);

            await RunRenewals();

            Assert.Empty(ChargesOf(subscription));
        }

        [Fact]
        public async Task Renew_TwoRoundsAtTheSameTime_ChargeOnce()
        {
            var (_, creatorId) = await CreatorWithPass();
            var (_, subscription) = await RenewingFan(creatorId);
            await MakeDue(subscription);

            await Task.WhenAll(RunRenewals(), RunRenewals(), RunRenewals());

            Assert.Single(ChargesOf(subscription));
        }

        [Theory]
        [InlineData(FakePaymentProvider.ChargeResult.Decline)]
        [InlineData(FakePaymentProvider.ChargeResult.Refuse)]
        public async Task Renew_CardDeclined_IsTriedAgainTomorrow_ThenTheSubscriptionEnds(
            FakePaymentProvider.ChargeResult result)
        {
            var (_, creatorId) = await CreatorWithPass();
            var (fan, subscription) = await RenewingFan(creatorId);
            var passUntil = (await PageOf(creatorId, fan)).PassValidUntil;
            Provider.NextCharge = result;
            try
            {
                // first failure: told, tried again in a day
                await MakeDue(subscription);
                var summary = await RunRenewals();
                var once = await Reload(subscription);
                Assert.True(summary.Failed >= 1);
                Assert.Equal(SubscriptionStatus.Active, once.Status);
                Assert.Equal(1, once.FailedAttempts);
                Assert.InRange((once.NextChargeAt - DateTimeOffset.UtcNow).TotalHours, 23, 25);
                Assert.Contains(Mail.Sent, x => x.To == "fan@example.com" && x.Subject.Contains("did not go through"));

                // second and third failure: the subscription ends
                await MakeDue(subscription);
                await RunRenewals();
                await MakeDue(subscription);
                await RunRenewals();
            }
            finally
            {
                Provider.NextCharge = FakePaymentProvider.ChargeResult.Succeed;
            }

            var ended = await Reload(subscription);
            Assert.Equal(SubscriptionStatus.Ended, ended.Status);
            Assert.Equal(3, ChargesOf(subscription).Count());
            Assert.Contains(Mail.Sent, x => x.To == "fan@example.com" && x.Subject.Contains("not renewed any more"));
            // what was paid is still there
            Assert.Equal(passUntil, (await PageOf(creatorId, fan)).PassValidUntil);
        }

        [Fact]
        public async Task Renew_OutcomeNotKnown_IsNeverChargedTwice_AndSettlesWhenItIsKnown()
        {
            var (_, creatorId) = await CreatorWithPass();
            var (fan, subscription) = await RenewingFan(creatorId);
            var before = (await PageOf(creatorId, fan)).PassValidUntil!.Value;
            await MakeDue(subscription);
            Provider.NextCharge = FakePaymentProvider.ChargeResult.Unknown;
            try
            {
                await RunRenewals();
            }
            finally
            {
                Provider.NextCharge = FakePaymentProvider.ChargeResult.Succeed;
            }

            var waiting = await Reload(subscription);
            Assert.NotNull(waiting.PendingReference);

            // a later round: the provider still does not know -> no second charge
            await MakeDue(subscription);
            await RunRenewals();
            Assert.Single(ChargesOf(subscription));

            // the payment turns out to be paid
            Provider.Pay(waiting.PendingReference!, PassPrice);
            await MakeDue(subscription);
            await RunRenewals();

            Assert.Single(ChargesOf(subscription));
            Assert.Null((await Reload(subscription)).PendingReference);
            Assert.InRange(((await PageOf(creatorId, fan)).PassValidUntil!.Value - before).TotalDays, 29.9, 30.1);
        }

        [Fact]
        public async Task Renew_CreatorStoppedSellingThePass_TheSubscriptionEndsWithoutACharge()
        {
            var (host, creatorId) = await CreatorWithPass();
            var (_, subscription) = await RenewingFan(creatorId);
            (await SetPassPrice(host, null)).EnsureSuccessStatusCode();
            await MakeDue(subscription);

            await RunRenewals();

            Assert.Equal(SubscriptionStatus.Ended, (await Reload(subscription)).Status);
            Assert.Empty(ChargesOf(subscription));
        }

        [Theory]
        [InlineData(1_000_000L, 1_000_000L)] // the creator lowered the price: the person pays the lower price
        [InlineData(5_000_000L, PassPrice)] // the creator raised it: the person pays what was agreed
        public async Task Renew_AmountIsNeverMoreThanAgreed(long newPrice, long expectedCharge)
        {
            var (host, creatorId) = await CreatorWithPass();
            var (_, subscription) = await RenewingFan(creatorId);
            (await SetPassPrice(host, newPrice)).EnsureSuccessStatusCode();
            await MakeDue(subscription);

            await RunRenewals();

            Assert.Equal(expectedCharge, Assert.Single(ChargesOf(subscription)).AmountMinor);
        }

        [Fact]
        public async Task Cancel_StopsRenewing_ButThePassRuns()
        {
            var (_, creatorId) = await CreatorWithPass();
            var (fan, subscription) = await RenewingFan(creatorId);

            var response = await Client(fan).PostAsync(
                $"/v1/passes/subscriptions/{subscription.SubscriptionId}/cancel", null);
            await MakeDue(subscription);
            await RunRenewals();

            response.EnsureSuccessStatusCode();
            Assert.Equal(SubscriptionStatus.Cancelled, (await Reload(subscription)).Status);
            Assert.Empty(ChargesOf(subscription));
            Assert.True((await PageOf(creatorId, fan)).HasPass);
            Assert.Null(Assert.Single(await Read<PassDto[]>(await Client(fan).GetAsync("/v1/passes/mine")))
                .SubscriptionId);
        }

        [Fact]
        public async Task Cancel_SomebodyElsesSubscription_NotFound_AndItKeepsRenewing()
        {
            var (_, creatorId) = await CreatorWithPass();
            var (_, subscription) = await RenewingFan(creatorId);

            var response = await Client(NewUser("other")).PostAsync(
                $"/v1/passes/subscriptions/{subscription.SubscriptionId}/cancel", null);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(SubscriptionStatus.Active, (await Reload(subscription)).Status);
        }

        [Fact]
        public async Task Renew_MyPassesAndThePageShowWhenItRenews()
        {
            var (_, creatorId) = await CreatorWithPass();
            var (fan, subscription) = await RenewingFan(creatorId);

            var pass = Assert.Single(await Read<PassDto[]>(await Client(fan).GetAsync("/v1/passes/mine")));
            var page = await PageOf(creatorId, fan);

            Assert.Equal(subscription.SubscriptionId, pass.SubscriptionId);
            Assert.Equal(subscription.NextChargeAt, pass.RenewsAt);
            Assert.Equal(subscription.NextChargeAt, page.PassRenewsAt);
            Assert.True(page.AutoRenewAvailable);
        }

        [Fact]
        public async Task Renew_BuyingAgainWithRenewal_KeepsOneSubscription()
        {
            var (_, creatorId) = await CreatorWithPass();
            var (fan, first) = await RenewingFan(creatorId, "AUTH_old");

            var (_, second) = await RenewingFan(creatorId, "AUTH_new", fan);

            Assert.Equal(first.SubscriptionId, second.SubscriptionId);
            Assert.Equal("AUTH_new", second.AuthorizationCode);
            Assert.Single(await Subscriptions.FindActiveOfUser(fan.Sub));
        }

        [Fact]
        public async Task Renew_ComingRenewal_ThePersonIsToldOnce()
        {
            var (_, creatorId) = await CreatorWithPass();
            var (_, subscription) = await RenewingFan(creatorId);
            var soon = (await Subscriptions.FindById(subscription.SubscriptionId))!;
            soon.NextChargeAt = DateTimeOffset.UtcNow.AddDays(2);
            await Subscriptions.Update(soon);
            var before = Mail.Sent.Count(x => x.Subject.Contains("renews soon"));

            await RunRenewals();
            await RunRenewals();

            Assert.Equal(before + 1, Mail.Sent.Count(x => x.Subject.Contains("renews soon")));
            var mail = Mail.Sent.Last(x => x.Subject.Contains("renews soon"));
            Assert.Contains("/tickets", mail.Text);
            Assert.Contains("20,000 NGN", mail.Text);
            Assert.Empty(ChargesOf(subscription));
        }

        // ---- the payments page of the administrators

        private HttpClient AdminClient(string role = "serveradmin")
        {
            var client = Factory.CreateClient();
            var claims = new List<System.Security.Claims.Claim>
            {
                new(System.Security.Claims.ClaimTypes.NameIdentifier, "admin-" + Guid.NewGuid().ToString("N")[..6]),
                new("name", "Admin"), new("role", role),
            };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                Factory.JwtTokens.GenerateJwtToken(claims));
            return client;
        }

        private async Task<AdminPaymentsDto> AdminPayments() =>
            await Read<AdminPaymentsDto>(await AdminClient().GetAsync("/v1/admin/payments"));

        [Theory]
        [InlineData("mod")]
        [InlineData("admin")]
        [InlineData("")]
        public async Task AdminPayments_OtherRoles_AreForbidden(string role)
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await AdminClient(role).GetAsync("/v1/admin/payments")).StatusCode);
        }

        [Fact]
        public async Task AdminPayments_NotSignedIn_Unauthorized_AndACreatorIsNotAnAdmin()
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await Client(null).GetAsync("/v1/admin/payments")).StatusCode);
            var host = await NewCreator();
            Assert.Equal(HttpStatusCode.Forbidden, (await Client(host).GetAsync("/v1/admin/payments")).StatusCode);
        }

        [Fact]
        public async Task AdminPayments_TotalsGrowWithSales_AndTheCommissionIsTenPercent()
        {
            var before = await AdminPayments();
            var (host, session) = await PublishedSession();
            await FanWithTicket(session);
            var (_, creatorId) = await CreatorWithPass();
            await FanWithPass(creatorId);

            var after = await AdminPayments();

            Assert.Equal(Price + PassPrice, after.Month.GrossMinor - before.Month.GrossMinor);
            Assert.Equal((Price + PassPrice) / 10, after.Month.PlatformMinor - before.Month.PlatformMinor);
            Assert.Equal((Price + PassPrice) * 9 / 10, after.Month.CreatorsMinor - before.Month.CreatorsMinor);
            Assert.Equal(1, after.Month.Tickets - before.Month.Tickets);
            Assert.Equal(1, after.Month.Passes - before.Month.Passes);
            Assert.Equal(1, after.Week.Tickets - before.Week.Tickets);
            Assert.True(after.Creators >= before.Creators + 2);
            Assert.Equal("NGN", after.Currency);
        }

        [Fact]
        public async Task AdminPayments_RecentOrdersShowWhoSoldWhatAndOnlyAMaskedBuyer()
        {
            var (host, session) = await PublishedSession();
            await FanWithTicket(session);

            var response = await AdminClient().GetAsync("/v1/admin/payments");
            var body = await response.Content.ReadAsStringAsync();
            var overview = JsonConvert.DeserializeObject<AdminPaymentsDto>(body, JsonConfig.Default)!;

            var row = overview.Recent.First(x => x.SessionTitle == session.Title);
            Assert.Equal("ticket", row.Kind);
            Assert.Equal("paid", row.Status);
            Assert.Equal("Ada Lovelace", row.CreatorName);
            Assert.Equal("f**@example.com", row.Buyer);
            Assert.DoesNotContain("fan@example.com", body);
        }

        [Fact]
        public async Task AdminPayments_AFailedPayment_ShowsInRecentWithItsProblem()
        {
            var (_, session) = await PublishedSession();
            var fan = NewUser("fan");
            var checkout = await Checkout(fan, session.SessionId);
            Provider.Fail(checkout.Reference!, session.PriceMinor);
            await Status(fan, checkout.Reference!);

            var row = (await AdminPayments()).Recent.First(x => x.SessionTitle == session.Title);

            Assert.Equal("failed", row.Status);
            Assert.NotNull(row.Problem);
        }

        [Fact]
        public async Task AdminPayments_RefundThatFailed_IsListedUntilItWorks()
        {
            // arrange: the provider refuses the refund
            var (host, session) = await PublishedSession();
            await FanWithTicket(session);
            Provider.RefuseRefunds = true;
            try
            {
                await Cancel(host, session.SessionId);
            }
            finally
            {
                Provider.RefuseRefunds = false;
            }

            // assert: an administrator sees it, with the reason
            var problem = Assert.Single((await AdminPayments()).RefundProblems, x => x.SessionTitle == session.Title);
            Assert.Equal(Price, problem.AmountMinor);
            Assert.Contains("Insufficient balance", problem.Reason);

            // the host tries again, it works: it is gone
            await Cancel(host, session.SessionId);
            Assert.DoesNotContain((await AdminPayments()).RefundProblems, x => x.SessionTitle == session.Title);
        }

        [Fact]
        public async Task AdminPayments_FailingRenewal_IsListed_AndSoIsTheOneThatEnded()
        {
            var (_, creatorId) = await CreatorWithPass();
            var (_, subscription) = await RenewingFan(creatorId);
            Provider.NextCharge = FakePaymentProvider.ChargeResult.Decline;
            try
            {
                await MakeDue(subscription);
                await RunRenewals();
                var failing = Assert.Single((await AdminPayments()).SubscriptionProblems,
                    x => x.SubscriptionId == subscription.SubscriptionId);
                Assert.Equal("active", failing.Status);
                Assert.Equal(1, failing.FailedAttempts);

                await MakeDue(subscription);
                await RunRenewals();
                await MakeDue(subscription);
                await RunRenewals();
            }
            finally
            {
                Provider.NextCharge = FakePaymentProvider.ChargeResult.Succeed;
            }

            var ended = Assert.Single((await AdminPayments()).SubscriptionProblems,
                x => x.SubscriptionId == subscription.SubscriptionId);
            Assert.Equal("ended", ended.Status);
            Assert.NotNull(ended.EndedAt);
        }

        [Fact]
        public async Task AdminPayments_ARenewalThatWorked_IsCountedAsARenewal()
        {
            var before = await AdminPayments();
            var (_, creatorId) = await CreatorWithPass();
            var (_, subscription) = await RenewingFan(creatorId);
            await MakeDue(subscription);
            await RunRenewals();

            var after = await AdminPayments();

            Assert.Equal(1, after.Month.Renewals - before.Month.Renewals);
            Assert.Equal(2, after.Month.Passes - before.Month.Passes);
            Assert.Contains(after.Recent, x => x.Kind == "renewal");
            Assert.True(after.ActiveSubscriptions >= 1);
        }

        [Fact]
        public async Task AdminPayments_NeverShowsACardToken()
        {
            var (_, creatorId) = await CreatorWithPass();
            await RenewingFan(creatorId, "AUTH_very_secret");

            var body = await AdminClient().GetStringAsync("/v1/admin/payments");

            Assert.DoesNotContain("AUTH_very_secret", body);
            Assert.DoesNotContain("uthorizationCode", body);
        }

        [Fact]
        public async Task Discover_ListsPublishedSessionsForEverybody_NotCancelledOnes()
        {
            // arrange
            var host = await NewCreator();
            var open = await Publish(host, title: "Open for tickets");
            var called = await Publish(host, title: "Called off");
            (await Client(host).PostAsync($"/v1/sessions/{called.SessionId}/cancel", null)).EnsureSuccessStatusCode();

            // act: nobody is signed in
            var list = await Read<DiscoverSessionDto[]>(await Client(null).GetAsync("/v1/sessions/discover"));

            // assert
            var entry = Assert.Single(list, x => x.SessionId == open.SessionId);
            Assert.Equal("Ada Lovelace", entry.CreatorName);
            Assert.False(entry.HasTicket);
            Assert.DoesNotContain(list, x => x.SessionId == called.SessionId);
        }

        [Fact]
        public async Task Discover_SignedInHost_IsMarkedAsHost()
        {
            var (host, session) = await PublishedSession();

            var list = await Read<DiscoverSessionDto[]>(await Client(host).GetAsync("/v1/sessions/discover"));

            Assert.True(Assert.Single(list, x => x.SessionId == session.SessionId).IsHost);
        }

        [Fact]
        public async Task Publish_Creator_GetsASessionWithARoomOfItsOwn()
        {
            var (host, session) = await PublishedSession();

            Assert.Equal(8, session.SessionId.Length);
            Assert.Equal("NGN", session.Currency);
            Assert.Equal(Price, session.PriceMinor);
            Assert.False(string.IsNullOrEmpty(session.ConferenceId));

            var mine = await Read<SessionDto[]>(await Client(host).GetAsync("/v1/sessions/mine"));
            Assert.Equal(session.SessionId, Assert.Single(mine).SessionId);
        }

        [Fact]
        public async Task Publish_TheRoomIsAStage_SpeakerLargeAndNoEmptyTilesForTheAudience()
        {
            var (host, session) = await PublishedSession();

            var data = await Read<Strive.Core.Services.ConferenceManagement.ConferenceData>(
                await Client(host).GetAsync($"/v1/conference/{session.ConferenceId}"));

            Assert.Equal(Strive.Core.Services.Scenes.SceneOptions.BasicSceneType.ActiveSpeaker,
                data.Configuration.Scenes.DefaultScene);
            Assert.True(data.Configuration.Scenes.HideParticipantsWithoutWebcam);
            Assert.Equal("Piano for beginners", data.Configuration.Name);
            Assert.Equal(new[] {host.Sub}, data.Configuration.Moderators);
        }

        [Fact]
        public async Task Publish_NotACreator_BadRequest()
        {
            var response = await Client(NewUser("nobody")).PostAsync("/v1/sessions",
                new JsonNetContent(new CreateSessionDto("A session", null, "teach", Price, null)));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("Payments_CreatorCannotSell", await response.Content.ReadAsStringAsync());
        }

        [Theory]
        [InlineData("ab", 500000L, "teach")]
        [InlineData("A fine title", 5000L, "teach")]
        [InlineData("A fine title", 5000000000L, "teach")]
        [InlineData("A fine title", 500000L, "nonsense")]
        public async Task Publish_InvalidDetails_BadRequest(string title, long price, string category)
        {
            var host = await NewCreator();

            var response = await Client(host).PostAsync("/v1/sessions",
                new JsonNetContent(new CreateSessionDto(title, null, category, price, null)));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // ---- the page of the session

        [Fact]
        public async Task Page_Anonymous_ShowsTheOfferButNotTheRoom()
        {
            var (_, session) = await PublishedSession();

            var page = await Read<PublicSessionDto>(await Client(null).GetAsync($"/v1/sessions/{session.SessionId}"));

            Assert.Equal("Piano for beginners", page.Title);
            Assert.Equal("Ada Lovelace", page.Creator.DisplayName);
            Assert.False(page.HasTicket);
            Assert.False(page.IsHost);
            Assert.Null(page.ConferenceId);
        }

        [Fact]
        public async Task Page_Host_SeesTheRoom()
        {
            var (host, session) = await PublishedSession();

            var page = await Read<PublicSessionDto>(await Client(host).GetAsync($"/v1/sessions/{session.SessionId}"));

            Assert.True(page.IsHost);
            Assert.Equal(session.ConferenceId, page.ConferenceId);
        }

        [Fact]
        public async Task Page_Unknown_NotFound()
        {
            var response = await Client(null).GetAsync("/v1/sessions/zzzzzzzz");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // ---- checkout

        [Fact]
        public async Task Checkout_SendsThePlatformCommissionAndTheSubaccountOfTheCreator()
        {
            var (_, session) = await PublishedSession();
            var fan = NewUser("fan");

            var checkout = await Checkout(fan, session.SessionId);

            Assert.StartsWith("https://checkout.test/gbd_", checkout.AuthorizationUrl);
            var sent = Provider.Checkouts.Last(x => x.Reference == checkout.Reference);
            Assert.Equal(Price, sent.AmountMinor);
            Assert.Equal("NGN", sent.Currency);
            // 10% of ₦5,000 stays with the platform
            Assert.Equal(50_000, sent.PlatformAmountMinor);
            Assert.StartsWith("ACCT_fake", sent.SubaccountCode);
            Assert.Equal("fan@example.com", sent.Email);
            Assert.EndsWith($"/s/{session.SessionId}", sent.CallbackUrl);
        }

        [Fact]
        public async Task Checkout_TheHost_CannotBuyAFanTicket()
        {
            var (host, session) = await PublishedSession();

            var response = await Client(host).PostAsync($"/v1/sessions/{session.SessionId}/checkout",
                new JsonNetContent(new CheckoutDto("host@example.com")));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("Payments_HostCannotBuy", await response.Content.ReadAsStringAsync());
        }

        [Theory]
        [InlineData("not an email")]
        [InlineData("")]
        public async Task Checkout_InvalidEmail_BadRequest(string email)
        {
            var (_, session) = await PublishedSession();

            var response = await Client(NewUser("fan")).PostAsync($"/v1/sessions/{session.SessionId}/checkout",
                new JsonNetContent(new CheckoutDto(email)));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Checkout_NotSignedIn_Unauthorized()
        {
            var (_, session) = await PublishedSession();

            var response = await Client(null).PostAsync($"/v1/sessions/{session.SessionId}/checkout",
                new JsonNetContent(new CheckoutDto("a@b.co")));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Checkout_CancelledSession_BadRequest()
        {
            var (host, session) = await PublishedSession();
            (await Client(host).PostAsync($"/v1/sessions/{session.SessionId}/cancel", null)).EnsureSuccessStatusCode();

            var response = await Client(NewUser("fan")).PostAsync($"/v1/sessions/{session.SessionId}/checkout",
                new JsonNetContent(new CheckoutDto("a@b.co")));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("Payments_SessionCancelled", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Checkout_ProviderDown_BadRequestWithAClearCode()
        {
            var (_, session) = await PublishedSession();
            Provider.Unavailable = true;
            try
            {
                var response = await Client(NewUser("fan")).PostAsync($"/v1/sessions/{session.SessionId}/checkout",
                    new JsonNetContent(new CheckoutDto("a@b.co")));

                Assert.Contains("Payments_ProviderUnavailable", await response.Content.ReadAsStringAsync());
            }
            finally
            {
                Provider.Unavailable = false;
            }
        }

        // ---- paying and the ticket

        [Fact]
        public async Task Paid_AfterTheProviderConfirms_TheFanHasATicket()
        {
            var (_, session) = await PublishedSession();
            var fan = NewUser("fan");
            var checkout = await Checkout(fan, session.SessionId);

            Assert.Equal("pending", (await Status(fan, checkout.Reference!)).Status);

            Provider.Pay(checkout.Reference!, Price);
            var status = await Status(fan, checkout.Reference!);

            Assert.Equal("paid", status.Status);
            Assert.True(status.HasTicket);
            Assert.Equal(session.ConferenceId, status.ConferenceId);

            var page = await Read<PublicSessionDto>(await Client(fan).GetAsync($"/v1/sessions/{session.SessionId}"));
            Assert.True(page.HasTicket);
            Assert.Equal(session.ConferenceId, page.ConferenceId);
        }

        [Fact]
        public async Task Paid_WrongAmount_NoTicket()
        {
            var (_, session) = await PublishedSession();
            var fan = NewUser("fan");
            var checkout = await Checkout(fan, session.SessionId);

            // the fan paid less than the price: whatever the browser says, the order does not count
            Provider.Pay(checkout.Reference!, 100);
            var status = await Status(fan, checkout.Reference!);

            Assert.Equal("failed", status.Status);
            Assert.False(status.HasTicket);
            Assert.Null(status.ConferenceId);
        }

        [Fact]
        public async Task Paid_WrongCurrency_NoTicket()
        {
            var (_, session) = await PublishedSession();
            var fan = NewUser("fan");
            var checkout = await Checkout(fan, session.SessionId);

            Provider.Pay(checkout.Reference!, Price, "USD");

            Assert.Equal("failed", (await Status(fan, checkout.Reference!)).Status);
        }

        [Fact]
        public async Task Paid_TheProviderSaysItFailed_NoTicket()
        {
            var (_, session) = await PublishedSession();
            var fan = NewUser("fan");
            var checkout = await Checkout(fan, session.SessionId);

            Provider.Fail(checkout.Reference!, Price);

            var status = await Status(fan, checkout.Reference!);
            Assert.Equal("failed", status.Status);
            Assert.False(status.HasTicket);
        }

        [Fact]
        public async Task Status_OfSomebodyElsesPayment_NotFound()
        {
            var (_, session) = await PublishedSession();
            var fan = NewUser("fan");
            var checkout = await Checkout(fan, session.SessionId);

            var response = await Client(NewUser("other")).GetAsync($"/v1/payments/{checkout.Reference}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Checkout_FanWhoHasATicket_DoesNotPayTwice()
        {
            var (_, session) = await PublishedSession();
            var fan = await FanWithTicket(session);
            var checkoutsBefore = Provider.Checkouts.Count;

            var again = await Checkout(fan, session.SessionId);

            Assert.True(again.AlreadyHasTicket);
            Assert.Null(again.AuthorizationUrl);
            Assert.Equal(checkoutsBefore, Provider.Checkouts.Count);
        }

        [Fact]
        public async Task Settling_Twice_GrantsOneTicket()
        {
            var (_, session) = await PublishedSession();
            var fan = NewUser("fan");
            var checkout = await Checkout(fan, session.SessionId);
            Provider.Pay(checkout.Reference!, Price);

            await Task.WhenAll(Status(fan, checkout.Reference!), Status(fan, checkout.Reference!),
                Status(fan, checkout.Reference!));

            var tickets = await Read<TicketDto[]>(await Client(fan).GetAsync("/v1/tickets/mine"));
            Assert.Single(tickets);
            Assert.Equal("Piano for beginners", tickets[0].Title);
            Assert.Equal("Ada Lovelace", tickets[0].CreatorName);
        }

        // ---- the notification of the provider

        [Fact]
        public async Task Webhook_ChargeSuccess_GrantsTheTicketWithoutTheBrowser()
        {
            var (_, session) = await PublishedSession();
            var fan = NewUser("fan");
            var checkout = await Checkout(fan, session.SessionId);
            Provider.Pay(checkout.Reference!, Price);

            var response = await Webhook($"{{\"event\":\"charge.success\",\"data\":{{\"reference\":\"{checkout.Reference}\"}}}}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var tickets = await Read<TicketDto[]>(await Client(fan).GetAsync("/v1/tickets/mine"));
            Assert.Single(tickets);
        }

        [Fact]
        public async Task Webhook_WrongSignature_Unauthorized_AndNothingHappens()
        {
            var (_, session) = await PublishedSession();
            var fan = NewUser("fan");
            var checkout = await Checkout(fan, session.SessionId);
            Provider.Pay(checkout.Reference!, Price);

            var response = await Webhook($"{{\"event\":\"charge.success\",\"data\":{{\"reference\":\"{checkout.Reference}\"}}}}",
                "00" + new string('a', 126));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Empty(await Read<TicketDto[]>(await Client(fan).GetAsync("/v1/tickets/mine")));
        }

        [Fact]
        public async Task Webhook_TheSameMessageTwice_OneTicket()
        {
            var (_, session) = await PublishedSession();
            var fan = NewUser("fan");
            var checkout = await Checkout(fan, session.SessionId);
            Provider.Pay(checkout.Reference!, Price);
            var json = $"{{\"event\":\"charge.success\",\"data\":{{\"reference\":\"{checkout.Reference}\"}}}}";

            Assert.Equal(HttpStatusCode.OK, (await Webhook(json)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Webhook(json)).StatusCode);

            Assert.Single(await Read<TicketDto[]>(await Client(fan).GetAsync("/v1/tickets/mine")));
        }

        [Fact]
        public async Task Webhook_UnknownReference_IsAnsweredWith200SoThatItIsNotRepeated()
        {
            var response = await Webhook("{\"event\":\"charge.success\",\"data\":{\"reference\":\"gbd_unknown\"}}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Webhook_OtherEvents_AreIgnored()
        {
            var response = await Webhook("{\"event\":\"transfer.success\",\"data\":{\"reference\":\"x\"}}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Webhook_ChargeSuccessThatTheProviderDoesNotConfirm_NoTicket()
        {
            // a signed message says paid, but the provider does not know the payment: nothing is granted
            var (_, session) = await PublishedSession();
            var fan = NewUser("fan");
            var checkout = await Checkout(fan, session.SessionId);

            await Webhook($"{{\"event\":\"charge.success\",\"data\":{{\"reference\":\"{checkout.Reference}\"}}}}");

            Assert.Empty(await Read<TicketDto[]>(await Client(fan).GetAsync("/v1/tickets/mine")));
        }

        // ---- the room

        private ConferenceCreatedResponseDto Room(SessionDto session) => new(session.ConferenceId);

        [Fact]
        public async Task Room_FanWithoutTicket_CannotJoin()
        {
            var (host, session) = await PublishedSession();
            var hostConnection = await ConnectUserToConference(host, Room(session));
            AssertSuccess(await OpenConference(hostConnection));

            await AssertCannotJoin(NewUser("fan"), session);
        }

        [Fact]
        public async Task Room_FanWithTicket_CanJoin()
        {
            var (host, session) = await PublishedSession();
            var hostConnection = await ConnectUserToConference(host, Room(session));
            AssertSuccess(await OpenConference(hostConnection));
            var fan = await FanWithTicket(session);

            var connection = await ConnectUserToConference(fan, Room(session));

            Assert.NotNull(connection);
        }

        [Fact]
        public async Task Room_TheHostCanAlwaysJoin()
        {
            var (host, session) = await PublishedSession();

            var connection = await ConnectUserToConference(host, Room(session));

            AssertSuccess(await OpenConference(connection));
        }

        [Fact]
        public async Task Room_CancelledSession_FansWithATicketCannotJoinEither()
        {
            var (host, session) = await PublishedSession();
            var hostConnection = await ConnectUserToConference(host, Room(session));
            AssertSuccess(await OpenConference(hostConnection));
            var fan = await FanWithTicket(session);
            (await Client(host).PostAsync($"/v1/sessions/{session.SessionId}/cancel", null)).EnsureSuccessStatusCode();

            await AssertCannotJoin(fan, session);
        }

        [Fact]
        public async Task Room_AnOrdinaryConference_IsNotAffected()
        {
            var (connection, _) = await ConnectToOpenedConference();

            Assert.NotNull(connection);
        }

        private async Task AssertCannotJoin(UserAccount account, SessionDto session)
        {
            var failed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                var connection = await ConnectUserToConference(account, Room(session), false,
                    hub => hub.On<object>(CoreHubMessages.OnConnectionError, _ => failed.TrySetResult(true)));
                try
                {
                    await connection.Hub.InvokeAsync(nameof(CoreHub.FetchPermissions), (string?) null);
                }
                finally
                {
                    await connection.Hub.DisposeAsync();
                }
            });
        }
    }
}
