using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    ///     In production the sign in pages are served under /account of the address of the app (meet.example.com/account),
    ///     so that people only see one domain.
    /// </summary>
    public class PathBaseTests : IClassFixture<MongoFixture>, IDisposable
    {
        private const string App = "https://meet.test";
        private readonly MongoFixture _mongo;
        private readonly List<IDisposable> _disposables = new();

        public PathBaseTests(MongoFixture mongo)
        {
            _mongo = mongo;
        }

        public void Dispose()
        {
            foreach (var d in _disposables) d.Dispose();
        }

        private class Factory : WebApplicationFactory<Program>
        {
            private readonly string _mongo;
            private readonly string? _pathBase;

            public Factory(string mongo, string? pathBase)
            {
                _mongo = mongo;
                _pathBase = pathBase;
            }

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.UseEnvironment("Production");
                var settings = new Dictionary<string, string?>
                {
                    ["IdentityServer:SpaClientHost"] = App,
                    ["IdentityServer:Issuer"] = _pathBase == null ? "https://meet.test" : "https://meet.test" + _pathBase,
                    ["Accounts:Mode"] = "Accounts",
                    ["Accounts:RequireEmailConfirmation"] = "false",
                    ["Accounts:MongoDb:ConnectionString"] = _mongo,
                    ["Accounts:MongoDb:DatabaseName"] = "pb" + Guid.NewGuid().ToString("N"),
                    ["Accounts:Email:Host"] = "smtp.test",
                };
                if (_pathBase != null) settings["IdentityServer:PathBase"] = _pathBase;
                builder.ConfigureAppConfiguration(c => c.AddInMemoryCollection(settings));
                builder.ConfigureServices(s => s.AddSingleton<IEmailSender>(new RecordingEmailSender()));
            }
        }

        private HttpClient Start(string? pathBase = "/account")
        {
            var factory = new Factory(_mongo.Runner.ConnectionString, pathBase);
            _disposables.Add(factory);
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false, BaseAddress = new Uri("https://meet.test"), HandleCookies = true,
            });
            _disposables.Add(client);
            return client;
        }

        [Fact]
        public async Task Pages_AreServedUnderThePrefix_AndLinkToThePrefix()
        {
            var client = Start();

            var response = await client.GetAsync("/account/Account/Login");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("href=\"/account/css/site.css\"", html);
            Assert.Contains("href=\"/account/Registration/Register", html);
            // no link to the root of the identity service: that is the web app
            Assert.DoesNotContain("href=\"/\"", html);
            Assert.Contains($"href=\"{App}/\"", html);

            // forms that name their action point to the prefix too
            var register = await client.GetStringAsync("/account/Registration/Register");
            Assert.Contains("action=\"/account/Registration/Register\"", register);
        }

        [Fact]
        public async Task StaticFiles_AreServedUnderThePrefix()
        {
            var client = Start();

            var css = await client.GetAsync("/account/css/site.css");

            Assert.Equal(HttpStatusCode.OK, css.StatusCode);
            Assert.Contains("--violet", await css.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Discovery_UsesTheAddressOfTheApp()
        {
            var client = Start();

            var json = await client.GetFromJsonAsync<JsonElement>("/account/.well-known/openid-configuration");

            Assert.Equal("https://meet.test/account", json.GetProperty("issuer").GetString());
            Assert.Equal("https://meet.test/account/connect/authorize", json.GetProperty("authorization_endpoint").GetString());
            Assert.StartsWith("https://meet.test/account/", json.GetProperty("jwks_uri").GetString());
        }

        [Fact]
        public async Task Discovery_WithoutThePrefix_StillWorksForInternalCalls()
        {
            // the API reads the keys over the docker network, without the prefix
            var client = Start();

            var response = await client.GetAsync("/.well-known/openid-configuration");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task WithoutAPrefix_EverythingIsAsBefore()
        {
            var client = Start(pathBase: null);

            var html = await client.GetStringAsync("/Account/Login");

            Assert.Contains("href=\"/Registration/Register", html);
            Assert.Contains("href=\"/css/site.css\"", html);
        }

        [Fact]
        public async Task SessionCookie_BelongsToThePrefix()
        {
            var client = Start();
            var token = Regex.Match(await client.GetStringAsync("/account/Registration/Register"),
                "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

            var response = await client.PostAsync("/account/Registration/Register", new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["Email"] = "pb@example.com", ["DisplayName"] = "Pb", ["Password"] = "12345678",
                    ["__RequestVerificationToken"] = token,
                }));

            // no confirmation needed here: signed in, sent back to the app root (not to the root of the identity service)
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal($"{App}/", response.Headers.Location!.ToString());
            var session = response.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("idsrv="));
            Assert.Contains("path=/account", session, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Logout_GoesStraightBackToTheApp_WithoutAQuestion()
        {
            var client = Start();

            var response = await client.GetAsync("/account/Account/Logout");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            // signed out page that redirects by itself (no "are you sure?")
            Assert.Contains("signed out", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Would you like to logout", html, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task LoginPage_TellsWhatStriveOffers()
        {
            var client = Start();

            var html = await client.GetStringAsync("/account/Account/Login");

            Assert.Contains("Meetings that feel", html);
            Assert.Contains("Watch a YouTube video together", html);
            Assert.Contains("Share your screen, with sound", html);
            Assert.Contains("Record and share", html);
        }
    }
}
