using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Identity.API.Accounts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Identity.API.Tests
{
    public class AdminOptionsTests
    {
        [Theory]
        [InlineData("boss@example.com", "boss@example.com", true)]
        [InlineData("boss@example.com, other@example.com", "OTHER@Example.com", true)]
        [InlineData("boss@example.com;other@example.com", " other@example.com ", true)]
        [InlineData("boss@example.com", "intruder@example.com", false)]
        [InlineData("boss@example.com", "boss@example.com.evil.com", false)]
        [InlineData("", "boss@example.com", false)]
        [InlineData("boss@example.com", "", false)]
        [InlineData("boss@example.com", null, false)]
        public void IsAdmin_MatchesTheListOfEmails(string list, string? email, bool expected)
        {
            Assert.Equal(expected, new AccountsOptions {AdminEmails = list}.IsAdmin(email));
        }
    }

    public class AdminStatsTests
    {
        [Fact]
        public async Task Stats_CountsUsersAndSignupsPerDay()
        {
            var repo = new InMemoryUserRepository();
            var now = new DateTimeOffset(2026, 3, 10, 15, 0, 0, TimeSpan.Zero);
            await repo.TryInsertAsync(new StriveUser
            {
                Id = "1", Email = "a@x.com", NormalizedEmail = "A@X.COM", EmailConfirmed = true, PasswordHash = "h", CreatedAt = now,
            });
            await repo.TryInsertAsync(new StriveUser
            {
                Id = "2", Email = "b@x.com", NormalizedEmail = "B@X.COM", EmailConfirmed = true, CreatedAt = now.AddDays(-2),
            });
            await repo.TryInsertAsync(new StriveUser
            {
                Id = "3", Email = "c@x.com", NormalizedEmail = "C@X.COM", CreatedAt = now.AddDays(-30),
            });
            await repo.TryAddLoginAsync("Google", "g1", "2");

            var stats = await repo.GetStatsAsync(14, now);

            Assert.Equal(3, stats.TotalUsers);
            Assert.Equal(2, stats.ConfirmedUsers);
            Assert.Equal(1, stats.WithPassword);
            Assert.Equal(1, stats.WithGoogle);
            Assert.Equal(14, stats.Signups.Count);
            Assert.Equal("2026-02-25", stats.Signups.First().Date);
            Assert.Equal("2026-03-10", stats.Signups.Last().Date);
            Assert.Equal(1, stats.Signups.Last().Count);
            Assert.Equal(1, stats.Signups.Single(x => x.Date == "2026-03-08").Count);
            Assert.Equal(2, stats.Signups.Sum(x => x.Count));
        }
    }

    public class AdminEndpointTests : IClassFixture<MongoFixture>, IDisposable
    {
        private readonly MongoFixture _mongo;
        private readonly List<IDisposable> _disposables = new();

        public AdminEndpointTests(MongoFixture mongo)
        {
            _mongo = mongo;
        }

        public void Dispose()
        {
            foreach (var d in _disposables) d.Dispose();
        }

        private HttpClient Start(string? apiKey, bool accounts = true)
        {
            var factory = new AdminFactory(_mongo.Runner.ConnectionString, apiKey, accounts);
            _disposables.Add(factory);
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false, BaseAddress = new Uri("https://identity.test"),
            });
            _disposables.Add(client);
            return client;
        }

        private static HttpRequestMessage Get(string? key)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/stats");
            if (key != null) request.Headers.Add("X-Api-Key", key);
            return request;
        }

        [Fact]
        public async Task Stats_WithTheApiKey_ReturnsTheNumbers()
        {
            var client = Start("shared-key");

            var response = await client.SendAsync(Get("shared-key"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(json.GetProperty("available").GetBoolean());
            Assert.Equal(0, json.GetProperty("totalUsers").GetInt32());
            Assert.Equal(14, json.GetProperty("signups").GetArrayLength());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("wrong")]
        [InlineData("shared-key ")]
        public async Task Stats_WrongOrMissingKey_IsUnauthorized(string? key)
        {
            var client = Start("shared-key");

            var response = await client.SendAsync(Get(key));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Stats_NoKeyConfigured_IsAlwaysUnauthorized()
        {
            var client = Start(null);

            Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Get(""))).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Get("anything"))).StatusCode);
        }

        [Fact]
        public async Task Stats_DemoMode_SaysThereAreNoAccounts()
        {
            var client = Start("shared-key", accounts: false);

            var response = await client.SendAsync(Get("shared-key"));

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(json.GetProperty("available").GetBoolean());
        }
    }

    public class AdminFactory : WebApplicationFactory<Program>
    {
        private readonly string _mongo;
        private readonly string? _apiKey;
        private readonly bool _accounts;

        public AdminFactory(string mongo, string? apiKey, bool accounts)
        {
            _mongo = mongo;
            _apiKey = apiKey;
            _accounts = accounts;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            var settings = new Dictionary<string, string?>
            {
                ["IdentityServer:SpaClientHost"] = "https://app.test",
                ["IdentityServer:Issuer"] = "https://identity.test",
                ["Accounts:Mode"] = _accounts ? "Accounts" : "Demo",
                ["Accounts:MongoDb:ConnectionString"] = _mongo,
                ["Accounts:MongoDb:DatabaseName"] = "admin" + Guid.NewGuid().ToString("N"),
                ["Accounts:Email:Host"] = "smtp.test",
            };
            if (_apiKey != null) settings["Accounts:AdminApiKey"] = _apiKey;
            builder.ConfigureAppConfiguration(c => c.AddInMemoryCollection(settings));
            builder.ConfigureServices(s => s.AddSingleton<IEmailSender>(new RecordingEmailSender()));
        }
    }

    public class ProfileServiceRoleTests
    {
        private static async Task<List<Claim>> Issue(params Claim[] subjectClaims)
        {
            var subject = new ClaimsPrincipal(new ClaimsIdentity(
                new[] {new Claim(JwtClaimTypes.Name, "Boss")}.Concat(subjectClaims), "test"));
            var context = new ProfileDataRequestContext {Subject = subject};
            await new ProfileService().GetProfileDataAsync(context);
            return context.IssuedClaims;
        }

        [Fact]
        public async Task TheRoleOfAServerAdministratorIsInTheToken()
        {
            var claims = await Issue(new Claim(JwtClaimTypes.Role, "serveradmin"));

            Assert.Contains(claims, x => x.Type == JwtClaimTypes.Role && x.Value == "serveradmin");
            Assert.Contains(claims, x => x.Type == JwtClaimTypes.Name && x.Value == "Boss");
        }

        [Fact]
        public async Task WithoutTheRoleThereIsNoRoleInTheToken()
        {
            var claims = await Issue();

            Assert.DoesNotContain(claims, x => x.Type == JwtClaimTypes.Role);
        }
    }
}
