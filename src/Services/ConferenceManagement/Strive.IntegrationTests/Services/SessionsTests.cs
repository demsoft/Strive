using System;
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
