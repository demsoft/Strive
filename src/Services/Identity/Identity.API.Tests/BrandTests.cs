using System;
using System.Collections.Generic;
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
    ///     One installation, two brands: gbedu.* is Gbedu, every other address is Strive. The sign in pages show the name,
    ///     the colours and the text of the brand of the address.
    /// </summary>
    public class BrandTests : IClassFixture<MongoFixture>, IDisposable
    {
        private readonly MongoFixture _mongo;
        private readonly List<IDisposable> _disposables = new();

        public BrandTests(MongoFixture mongo)
        {
            _mongo = mongo;
        }

        public void Dispose()
        {
            foreach (var d in _disposables) d.Dispose();
        }

        [Theory]
        [InlineData("gbedu.goserp.co.uk", "gbedu")]
        [InlineData("GBEDU.example.com", "gbedu")]
        [InlineData("meet.goserp.co.uk", "strive")]
        [InlineData("localhost", "strive")]
        [InlineData(null, "strive")]
        public void ForHost_BrandOfTheAddress(string? host, string expected)
        {
            Assert.Equal(expected, BrandInfo.ForHost(host));
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
                    ["Accounts:MongoDb:DatabaseName"] = "br" + Guid.NewGuid().ToString("N"),
                    ["Accounts:Email:Host"] = "smtp.test",
                };
                builder.ConfigureAppConfiguration(c => c.AddInMemoryCollection(settings));
                builder.ConfigureServices(s => s.AddSingleton<IEmailSender>(new RecordingEmailSender()));
            }
        }

        private async Task<string> LoginPage(string origin)
        {
            var factory = new Factory(_mongo.Runner.ConnectionString);
            _disposables.Add(factory);
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false, BaseAddress = new Uri(origin), HandleCookies = true,
            });
            _disposables.Add(client);

            var response = await client.GetAsync("/account/Account/Login");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadAsStringAsync();
        }

        [Fact]
        public async Task LoginPage_OnTheGbeduAddress_IsGbedu()
        {
            var html = await LoginPage("https://gbedu.test");

            Assert.Contains("<title>Gbedu</title>", html);
            Assert.Contains("data-brand=\"gbedu\"", html);
            Assert.Contains("aria-label=\"Gbedu\"", html);
            Assert.Contains("Go live.", html);
            // only what works today: payments are announced as coming, not offered
            Assert.Contains("coming soon", html);
            Assert.DoesNotContain("Meetings that feel", html);
        }

        [Fact]
        public async Task LoginPage_OnTheMainAddress_StaysStrive()
        {
            var html = await LoginPage("https://meet.test");

            Assert.Contains("<title>Strive</title>", html);
            Assert.Contains("data-brand=\"strive\"", html);
            Assert.Contains("Meetings that feel", html);
            Assert.DoesNotContain("Gbedu", html);
        }
    }
}
