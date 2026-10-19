using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Identity.API.Accounts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Identity.API.Tests
{
    /// <summary>
    ///     The web app can answer on more than one address (another brand on its own host name). The sign in has to work
    ///     from each of them and lead back to the address the person came from.
    /// </summary>
    public class ExtraHostTests : IClassFixture<MongoFixture>, IDisposable
    {
        private readonly MongoFixture _mongo;
        private readonly List<IDisposable> _disposables = new();

        public ExtraHostTests(MongoFixture mongo)
        {
            _mongo = mongo;
        }

        public void Dispose()
        {
            foreach (var d in _disposables) d.Dispose();
        }

        [Fact]
        public void ParseExtraHosts_NamesBecomeHttpsOrigins()
        {
            var result = AppUrls.ParseExtraHosts("gbedu.example.com, https://arena.example.com/ ;gbedu.example.com");

            Assert.Equal(new[] {"https://gbedu.example.com", "https://arena.example.com"}, result);
        }

        [Fact]
        public void ParseExtraHosts_Nothing_NoHosts()
        {
            Assert.Empty(AppUrls.ParseExtraHosts(null));
            Assert.Empty(AppUrls.ParseExtraHosts("  "));
        }

        [Fact]
        public void SpaClient_AcceptsTheRedirectAddressesOfEveryHost()
        {
            var client = Config.BuildSpaClient("https://meet.example.com", new[] {"https://gbedu.example.com"});

            Assert.Contains("https://meet.example.com/authentication/callback", client.RedirectUris);
            Assert.Contains("https://gbedu.example.com/authentication/callback", client.RedirectUris);
            Assert.Contains("https://gbedu.example.com/authentication/silent_callback", client.RedirectUris);
            Assert.Contains("https://gbedu.example.com/", client.PostLogoutRedirectUris);
            Assert.Contains("https://gbedu.example.com", client.AllowedCorsOrigins);
        }

        [Fact]
        public void SpaClient_WithoutExtraHosts_OnlyTheMainHost()
        {
            var client = Config.BuildSpaClient("https://meet.example.com");

            Assert.Equal(2, client.RedirectUris.Count);
            Assert.Single(client.PostLogoutRedirectUris);
        }

        private class Factory : WebApplicationFactory<Program>
        {
            private readonly string _mongo;

            public Factory(string mongo)
            {
                _mongo = mongo;
            }

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.UseEnvironment("Production");
                var settings = new Dictionary<string, string?>
                {
                    ["IdentityServer:SpaClientHost"] = "https://meet.test",
                    ["IdentityServer:ExtraSpaHosts"] = "gbedu.test",
                    ["IdentityServer:Issuer"] = "https://meet.test/account",
                    ["IdentityServer:PathBase"] = "/account",
                    ["Accounts:Mode"] = "Accounts",
                    ["Accounts:RequireEmailConfirmation"] = "false",
                    ["Accounts:MongoDb:ConnectionString"] = _mongo,
                    ["Accounts:MongoDb:DatabaseName"] = "eh" + Guid.NewGuid().ToString("N"),
                    ["Accounts:Email:Host"] = "smtp.test",
                };
                builder.ConfigureAppConfiguration(c => c.AddInMemoryCollection(settings));
                builder.ConfigureServices(s => s.AddSingleton<IEmailSender>(new RecordingEmailSender()));
            }
        }

        private HttpClient Start(string origin)
        {
            var factory = new Factory(_mongo.Runner.ConnectionString);
            _disposables.Add(factory);
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false, BaseAddress = new Uri(origin), HandleCookies = true,
            });
            _disposables.Add(client);
            return client;
        }

        [Theory]
        [InlineData("https://gbedu.test", "https://gbedu.test/")]
        [InlineData("https://meet.test", "https://meet.test/")]
        public async Task LoginPage_LinksBackToTheAddressThePersonCameFrom(string origin, string expectedHome)
        {
            var client = Start(origin);

            var response = await client.GetAsync("/account/Account/Login");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains($"href=\"{expectedHome}\"", html);
        }

        [Fact]
        public async Task Discovery_FromTheExtraHost_AnswersWithTheEndpointsOfThatHost()
        {
            var client = Start("https://gbedu.test");

            var json = await client.GetStringAsync("/account/.well-known/openid-configuration");

            Assert.Contains("https://gbedu.test/account/connect/authorize", json);
            // the tokens keep the issuer of the main host: the API checks that one
            Assert.Contains("\"issuer\":\"https://meet.test/account\"", json);
        }
    }
}
