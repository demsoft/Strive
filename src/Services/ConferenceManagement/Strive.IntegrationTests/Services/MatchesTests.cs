using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Strive.Infrastructure.Serialization;
using Strive.IntegrationTests._Helpers;
using Strive.Models.Response;
using Xunit;
using Xunit.Abstractions;

namespace Strive.IntegrationTests.Services
{
    /// <summary>
    ///     Arena: a match is a session with teams and a declaration of the right to stream it. People can report it, a
    ///     moderator takes it down (tickets refunded) or keeps it.
    /// </summary>
    [Collection(IntegrationTestCollection.Definition)]
    public class MatchesTests : ServiceIntegrationTest
    {
        private const long Price = 300_000;

        public MatchesTests(ITestOutputHelper testOutputHelper, MongoDbFixture mongoDb) : base(testOutputHelper, mongoDb)
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

        private HttpClient AdminApi()
        {
            var client = Factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                Factory.JwtTokens.GenerateJwtToken(new System.Collections.Generic.List<System.Security.Claims.Claim>
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

        private async Task<UserAccount> NewOrganizer()
        {
            var host = NewUser("org");
            (await Client(host).PutAsync("/v1/creators/me",
                new JsonNetContent(new SaveCreatorDto("Lagos League", "Football", "058", "0123456789"))))
                .EnsureSuccessStatusCode();
            return host;
        }

        private static MatchInputDto Match(string basis = "own", string? note = null, bool accepted = true,
            string home = "Lagos FC", string away = "Abuja United") =>
            new(home, away, "State Cup", basis, note, accepted);

        private Task<HttpResponseMessage> PostMatch(UserAccount host, MatchInputDto? match, string category = "match",
            string title = "") =>
            Client(host).PostAsync("/v1/sessions",
                new JsonNetContent(new CreateSessionDto(title, "Quarter final", category, Price, null, null, match)));

        private async Task<SessionDto> PublishMatch(UserAccount host) =>
            await Read<SessionDto>(await PostMatch(host, Match()));

        private async Task<UserAccount> FanWithTicket(SessionDto session)
        {
            var fan = NewUser("fan");
            var checkout = await Read<CheckoutResultDto>(await Client(fan).PostAsync(
                $"/v1/sessions/{session.SessionId}/checkout", new JsonNetContent(new CheckoutDto("fan@example.com"))));
            Provider.Pay(checkout.Reference!, session.PriceMinor);
            await Read<PaymentStatusDto>(await Client(fan).GetAsync($"/v1/payments/{checkout.Reference}"));
            return fan;
        }

        private Task<HttpResponseMessage> Report(UserAccount? user, string id, string reason = "illegal",
            string? details = null) =>
            Client(user).PostAsync($"/v1/sessions/{id}/report", new JsonNetContent(new ReportSessionDto(reason, details)));

        private async Task<AdminSessionReportDto[]> AdminReports() =>
            await Read<AdminSessionReportDto[]>(await AdminApi().GetAsync("/v1/admin/sessions/reports"));

        // ---- publishing a match

        [Fact]
        public async Task Publish_Match_ShowsTheTeamsAndTheTitleIsTheFixture()
        {
            var host = await NewOrganizer();

            var session = await PublishMatch(host);

            Assert.Equal("Lagos FC vs Abuja United", session.Title);
            Assert.Equal("match", session.Category);
            Assert.Equal("Lagos FC", session.Match!.HomeTeam);
            Assert.Equal("State Cup", session.Match.Competition);
            Assert.Equal("own", session.RightsBasis);

            var page = await Read<PublicSessionDto>(await Client(null).GetAsync($"/v1/sessions/{session.SessionId}"));
            Assert.Equal("Abuja United", page.Match!.AwayTeam);
            Assert.Equal("own", page.Match.RightsBasis);
            var listed = (await Read<DiscoverSessionDto[]>(await Client(null).GetAsync("/v1/sessions/discover")))
                .Single(x => x.SessionId == session.SessionId);
            Assert.Equal("Lagos FC", listed.Match!.HomeTeam);
        }

        [Fact]
        public async Task Publish_Match_WithoutTheDeclaration_IsRefused()
        {
            var host = await NewOrganizer();

            Assert.Equal(HttpStatusCode.BadRequest, (await PostMatch(host, Match(accepted: false))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await PostMatch(host, Match(basis: "licensed"))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await PostMatch(host, null)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await PostMatch(host, Match(), category: "music")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await PostMatch(host, Match(home: "x", away: "y"))).StatusCode);
        }

        [Fact]
        public async Task Publish_Match_WithALicence_KeepsTheNoteForModerators()
        {
            var host = await NewOrganizer();
            var session = await Read<SessionDto>(await PostMatch(host, Match("licensed", "Lagos State FA, licence 12")));
            var fan = NewUser("fan");

            (await Report(fan, session.SessionId)).EnsureSuccessStatusCode();

            var item = Assert.Single(await AdminReports(), x => x.SessionId == session.SessionId);
            Assert.Equal("licensed", item.RightsBasis);
            Assert.Equal("Lagos State FA, licence 12", item.RightsNote);
        }

        // ---- reports

        [Fact]
        public async Task Report_IsCountedOncePerPerson_AndTheSessionStaysUpUntilAModeratorDecides()
        {
            var host = await NewOrganizer();
            var session = await PublishMatch(host);
            var fan = NewUser("fan");

            (await Report(fan, session.SessionId, "rightsholder", "This is our match")).EnsureSuccessStatusCode();
            (await Report(fan, session.SessionId, "spam")).EnsureSuccessStatusCode();
            (await Report(NewUser("other"), session.SessionId, "illegal")).EnsureSuccessStatusCode();

            var item = Assert.Single(await AdminReports(), x => x.SessionId == session.SessionId);
            Assert.Equal(2, item.OpenReports);
            Assert.Equal(1, item.Reasons["rightsholder"]);
            Assert.Contains(item.Details, x => x.Text == "This is our match");
            Assert.Equal("Lagos FC", item.Match!.HomeTeam);
            var page = await Read<PublicSessionDto>(await Client(null).GetAsync($"/v1/sessions/{session.SessionId}"));
            Assert.Equal("Published", page.Status.ToString());
        }

        [Fact]
        public async Task Report_Refusals()
        {
            var host = await NewOrganizer();
            var session = await PublishMatch(host);

            Assert.Equal(HttpStatusCode.Unauthorized, (await Report(null, session.SessionId)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Report(host, session.SessionId)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Report(NewUser("f"), session.SessionId, "nonsense")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Report(NewUser("f"), "nope")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,
                (await Report(NewUser("f"), session.SessionId, details: new string('a', 501))).StatusCode);
        }

        [Fact]
        public async Task Admin_Endpoints_AreForServerAdminsOnly()
        {
            var fan = NewUser("fan");

            Assert.Equal(HttpStatusCode.Forbidden, (await Client(fan).GetAsync("/v1/admin/sessions/reports")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await Client(fan).PostAsync("/v1/admin/sessions/x/takedown", new JsonNetContent(new TakeDownDto(null))))
                .StatusCode);
        }

        // ---- decisions

        [Fact]
        public async Task TakeDown_CallsTheMatchOff_RefundsTheTickets_AndTellsTheOrganizer()
        {
            var host = await NewOrganizer();
            var session = await PublishMatch(host);
            var fan = await FanWithTicket(session);
            var reference = Provider.Checkouts.Last().Reference;
            (await Report(NewUser("rh"), session.SessionId, "rightsholder")).EnsureSuccessStatusCode();

            (await AdminApi().PostAsync($"/v1/admin/sessions/{session.SessionId}/takedown",
                new JsonNetContent(new TakeDownDto("Not your match to stream")))).EnsureSuccessStatusCode();

            Assert.Contains(Provider.Refunds, x => x.Reference == reference && x.AmountMinor == session.PriceMinor);
            var mine = Assert.Single(await Read<SessionDto[]>(await Client(host).GetAsync("/v1/sessions/mine")),
                x => x.SessionId == session.SessionId);
            Assert.Equal("Cancelled", mine.Status.ToString());
            Assert.Equal("Not your match to stream", mine.TakedownNote);
            Assert.Equal(1, mine.Refunded);
            Assert.True((await Read<TicketDto[]>(await Client(fan).GetAsync("/v1/tickets/mine"))).Single().Refunded);
            // decided: no longer in the list, nobody can buy again
            Assert.DoesNotContain(await AdminReports(), x => x.SessionId == session.SessionId);
            Assert.Equal(HttpStatusCode.BadRequest,
                (await Client(NewUser("late")).PostAsync($"/v1/sessions/{session.SessionId}/checkout",
                    new JsonNetContent(new CheckoutDto("late@example.com")))).StatusCode);
        }

        [Fact]
        public async Task TakeDown_UnknownSession_IsNotFound()
        {
            var response = await AdminApi().PostAsync("/v1/admin/sessions/nope/takedown",
                new JsonNetContent(new TakeDownDto(null)));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Keep_PutsTheReportsAside_AndTheMatchStaysOn()
        {
            var host = await NewOrganizer();
            var session = await PublishMatch(host);
            (await Report(NewUser("f"), session.SessionId, "misleading")).EnsureSuccessStatusCode();

            (await AdminApi().PostAsync($"/v1/admin/sessions/{session.SessionId}/keep", null)).EnsureSuccessStatusCode();

            Assert.DoesNotContain(await AdminReports(), x => x.SessionId == session.SessionId);
            var page = await Read<PublicSessionDto>(await Client(null).GetAsync($"/v1/sessions/{session.SessionId}"));
            Assert.Equal("Published", page.Status.ToString());
        }
    }
}
