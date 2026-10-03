using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Duende.IdentityServer;
using Identity.API.Accounts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Identity.API.Tests
{
    /// <summary>Stands in for Google: "signs in" as the claims that the test sets, without leaving the process.</summary>
    public class FakeGoogleHandler : IAuthenticationHandler
    {
        public static ClaimsPrincipal? Principal { get; set; }

        private HttpContext _context = null!;

        public Task InitializeAsync(AuthenticationScheme scheme, HttpContext context)
        {
            _context = context;
            return Task.CompletedTask;
        }

        public Task<AuthenticateResult> AuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());

        public async Task ChallengeAsync(AuthenticationProperties? properties)
        {
            await _context.SignInAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme, Principal!,
                properties);
            _context.Response.Redirect(properties!.RedirectUri!);
        }

        public Task ForbidAsync(AuthenticationProperties? properties) => Task.CompletedTask;
    }

    public class AccountsFactory : WebApplicationFactory<Program>
    {
        private readonly string _mongo;
        private readonly bool _accounts;

        public AccountsFactory(string mongo, bool accounts = true, string? allowedDomain = null)
        {
            _mongo = mongo;
            _accounts = accounts;
            AllowedDomain = allowedDomain;
        }

        public string? AllowedDomain { get; }
        public RecordingEmailSender Email { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            var settings = new Dictionary<string, string?>
            {
                ["IdentityServer:SpaClientHost"] = "https://app.test",
                ["IdentityServer:Issuer"] = "https://identity.test",
                ["Accounts:Mode"] = _accounts ? "Accounts" : "Demo",
                ["Accounts:MongoDb:ConnectionString"] = _mongo,
                ["Accounts:MongoDb:DatabaseName"] = "flow" + Guid.NewGuid().ToString("N"),
                ["Accounts:Email:Host"] = "smtp.test",
                ["Accounts:Google:ClientId"] = "fake-id",
                ["Accounts:Google:ClientSecret"] = "fake-secret",
            };
            if (AllowedDomain != null) settings["Accounts:AllowedEmailDomains"] = AllowedDomain;
            builder.ConfigureAppConfiguration(c => c.AddInMemoryCollection(settings));
            builder.ConfigureServices(s => s.AddSingleton<IEmailSender>(Email));
        }

        public HttpClient CreateBrowser()
        {
            // cookies are kept by the handler, redirects are followed by the test so that it can look at each step
            var client = CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false, BaseAddress = new Uri("https://identity.test"), HandleCookies = true,
            });

            // swap the Google handler for the fake one
            var schemes = Services.GetService<IAuthenticationSchemeProvider>();
            if (schemes?.GetSchemeAsync("Google").Result != null)
            {
                schemes.RemoveScheme("Google");
                schemes.AddScheme(new AuthenticationScheme("Google", "Google", typeof(FakeGoogleHandler)));
            }

            return client;
        }
    }

    public class AccountFlowTests : IClassFixture<MongoFixture>, IDisposable
    {
        private const string Password = "correct horse battery";
        private readonly MongoFixture _mongo;
        private readonly List<IDisposable> _disposables = new();

        public AccountFlowTests(MongoFixture mongo)
        {
            _mongo = mongo;
        }

        public void Dispose()
        {
            foreach (var d in _disposables) d.Dispose();
        }

        private (AccountsFactory factory, HttpClient browser) Start(bool accounts = true, string? allowedDomain = null)
        {
            var factory = new AccountsFactory(_mongo.Runner.ConnectionString, accounts, allowedDomain);
            var browser = factory.CreateBrowser();
            _disposables.Add(factory);
            _disposables.Add(browser);
            return (factory, browser);
        }

        private static async Task<string> TokenAsync(HttpClient browser, string url)
        {
            var html = await browser.GetStringAsync(url);
            return Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        }

        private static async Task<HttpResponseMessage> PostAsync(HttpClient browser, string formUrl, string postUrl,
            Dictionary<string, string> values)
        {
            values["__RequestVerificationToken"] = await TokenAsync(browser, formUrl);
            return await browser.PostAsync(postUrl, new FormUrlEncodedContent(values));
        }

        private static string Link(string mailBody) => Regex.Match(mailBody, @"https://identity\.test\S+").Value.TrimEnd('.');

        private static bool HasSessionCookie(HttpResponseMessage response) =>
            response.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(x => x.StartsWith("idsrv="));

        private static async Task<HttpResponseMessage> LoginAsync(HttpClient browser, string email, string password)
        {
            return await PostAsync(browser, "/Account/Login", "/Account/Login", new Dictionary<string, string>
            {
                ["Username"] = email, ["Password"] = password, ["button"] = "login", ["ReturnUrl"] = "",
            });
        }

        private static async Task RegisterAsync(HttpClient browser, string email, string name = "Ann")
        {
            var response = await PostAsync(browser, "/Registration/Register", "/Registration/Register",
                new Dictionary<string, string> {["Email"] = email, ["DisplayName"] = name, ["Password"] = Password});
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Registration/CheckEmail", response.Headers.Location!.ToString());
        }

        [Fact]
        public async Task Register_confirm_and_sign_in()
        {
            var (factory, browser) = Start();

            await RegisterAsync(browser, "ann@example.com");

            // not confirmed yet
            var early = await LoginAsync(browser, "ann@example.com", Password);
            var earlyHtml = await early.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, early.StatusCode);
            Assert.Contains("confirm your email", earlyHtml);
            Assert.Contains("Send the confirmation email again", earlyHtml);

            // opening the link only shows a button, the post confirms
            var link = Link(factory.Email.Last.TextBody);
            var page = await browser.GetAsync(new Uri(link).PathAndQuery);
            Assert.Contains("Confirm email address", await page.Content.ReadAsStringAsync());
            var token = Regex.Match(link, "token=([^&]+)").Groups[1].Value;
            var confirm = await PostAsync(browser, new Uri(link).PathAndQuery, "/Registration/ConfirmEmail",
                new Dictionary<string, string> {["Token"] = token});
            Assert.Equal(HttpStatusCode.Redirect, confirm.StatusCode);
            Assert.StartsWith("/Account/Login", confirm.Headers.Location!.ToString());

            var notice = await browser.GetStringAsync("/Account/Login");
            Assert.Contains("Your email address is confirmed", notice);

            var signedIn = await LoginAsync(browser, "ann@example.com", Password);
            Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
            Assert.True(HasSessionCookie(signedIn));
        }

        [Fact]
        public async Task A_wrong_password_shows_one_generic_error()
        {
            var (factory, browser) = Start();
            await RegisterAsync(browser, "bob@example.com");

            var response = await LoginAsync(browser, "bob@example.com", "not the password");
            var unknown = await LoginAsync(browser, "nobody@example.com", "not the password");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            var unknownHtml = await unknown.Content.ReadAsStringAsync();
            Assert.Contains("Invalid username or password", html);
            Assert.Contains("Invalid username or password", unknownHtml);
            Assert.False(HasSessionCookie(response));
        }

        [Fact]
        public async Task The_registration_page_gives_the_same_answer_for_a_known_email()
        {
            var (factory, browser) = Start();
            await RegisterAsync(browser, "carl@example.com");

            var again = await PostAsync(browser, "/Registration/Register", "/Registration/Register",
                new Dictionary<string, string>
                    {["Email"] = "carl@example.com", ["DisplayName"] = "Other", ["Password"] = Password});

            Assert.Equal(HttpStatusCode.Redirect, again.StatusCode);
            Assert.Contains("/Registration/CheckEmail", again.Headers.Location!.ToString());
        }

        [Fact]
        public async Task A_weak_password_is_explained()
        {
            var (_, browser) = Start();

            var response = await PostAsync(browser, "/Registration/Register", "/Registration/Register",
                new Dictionary<string, string> {["Email"] = "dan@example.com", ["DisplayName"] = "Dan", ["Password"] = "short"});

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("at least 10 characters", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Password_reset_by_email()
        {
            var (factory, browser) = Start();
            await RegisterAsync(browser, "eve@example.com");
            var confirmLink = Link(factory.Email.Last.TextBody);
            await PostAsync(browser, new Uri(confirmLink).PathAndQuery, "/Registration/ConfirmEmail",
                new Dictionary<string, string> {["Token"] = Regex.Match(confirmLink, "token=([^&]+)").Groups[1].Value});

            var forgot = await PostAsync(browser, "/Registration/ForgotPassword", "/Registration/ForgotPassword",
                new Dictionary<string, string> {["Email"] = "eve@example.com"});
            Assert.Contains("link to choose a new password", await forgot.Content.ReadAsStringAsync());

            var link = Link(factory.Email.Last.TextBody);
            var reset = await PostAsync(browser, new Uri(link).PathAndQuery, "/Registration/ResetPassword",
                new Dictionary<string, string>
                {
                    ["Token"] = Regex.Match(link, "token=([^&]+)").Groups[1].Value, ["Password"] = "a different long password",
                });
            Assert.Equal(HttpStatusCode.Redirect, reset.StatusCode);

            Assert.False(HasSessionCookie(await LoginAsync(browser, "eve@example.com", Password)));
            Assert.True(HasSessionCookie(await LoginAsync(browser, "eve@example.com", "a different long password")));
        }

        [Fact]
        public async Task A_forged_post_without_the_anti_forgery_token_is_rejected()
        {
            var (_, browser) = Start();

            var response = await browser.PostAsync("/Registration/Register", new FormUrlEncodedContent(
                new Dictionary<string, string> {["Email"] = "x@example.com", ["DisplayName"] = "X", ["Password"] = Password}));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // ---- Google

        private static void GoogleIs(string key, string email, string name, bool verified = true)
        {
            FakeGoogleHandler.Principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, key), new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Name, name), new Claim("email_verified", verified ? "True" : "False"),
            }, "Google"));
        }

        private static async Task<HttpResponseMessage> FollowAsync(HttpClient browser, HttpResponseMessage response)
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location!;
            return await browser.GetAsync(location.IsAbsoluteUri ? location.PathAndQuery : location.ToString());
        }

        [Fact]
        public async Task The_login_page_offers_google_only_when_it_is_configured()
        {
            var (_, browser) = Start();

            var html = await browser.GetStringAsync("/Account/Login");

            Assert.Contains("Continue with Google", html);
            Assert.Contains("Create an account", html);
        }

        [Fact]
        public async Task A_first_google_sign_in_asks_for_a_display_name_and_the_next_one_does_not()
        {
            var (_, browser) = Start();
            GoogleIs("g-100", "gina@gmail.com", "Gina Gee");

            var challenge = await browser.GetAsync("/External/Challenge");
            var callback = await FollowAsync(browser, challenge);
            Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
            Assert.Contains("/External/Welcome", callback.Headers.Location!.ToString());
            // no session yet: the name is missing
            Assert.False(HasSessionCookie(callback));

            var welcome = await FollowAsync(browser, callback);
            Assert.Contains("value=\"Gina Gee\"", await welcome.Content.ReadAsStringAsync());

            // an invalid name is explained
            var invalid = await PostAsync(browser, "/External/Welcome", "/External/Welcome",
                new Dictionary<string, string> {["DisplayName"] = "gina@gmail.com"});
            Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
            Assert.Contains("email address as your name", await invalid.Content.ReadAsStringAsync());

            var done = await PostAsync(browser, "/External/Welcome", "/External/Welcome",
                new Dictionary<string, string> {["DisplayName"] = "Gigi"});
            Assert.Equal(HttpStatusCode.Redirect, done.StatusCode);
            Assert.True(HasSessionCookie(done));

            // the second time Google sends the same account
            var second = await browser.GetAsync("/External/Challenge");
            var secondCallback = await FollowAsync(browser, second);
            Assert.Equal(HttpStatusCode.Redirect, secondCallback.StatusCode);
            Assert.True(HasSessionCookie(secondCallback));
            Assert.DoesNotContain("Welcome", secondCallback.Headers.Location!.ToString());
        }

        [Fact]
        public async Task The_welcome_page_is_not_reachable_without_a_google_sign_in()
        {
            var (_, browser) = Start();

            var response = await browser.GetAsync("/External/Welcome");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
        }

        [Fact]
        public async Task An_unverified_google_email_is_refused_with_a_message()
        {
            var (_, browser) = Start();
            GoogleIs("g-200", "mallory@gmail.com", "Mallory", verified: false);

            var callback = await FollowAsync(browser, await browser.GetAsync("/External/Challenge"));
            var login = await FollowAsync(browser, callback);

            Assert.Contains("has not verified", await login.Content.ReadAsStringAsync());
            Assert.False(HasSessionCookie(callback));
        }

        [Fact]
        public async Task Google_sign_up_with_a_domain_that_is_not_allowed_is_refused()
        {
            var (_, browser) = Start(allowedDomain: "example.com");
            GoogleIs("g-300", "gina@gmail.com", "Gina");

            var callback = await FollowAsync(browser, await browser.GetAsync("/External/Challenge"));
            var login = await FollowAsync(browser, callback);

            Assert.Contains("allowed domain", await login.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Registering_with_a_domain_that_is_not_allowed_is_refused()
        {
            var (_, browser) = Start(allowedDomain: "example.com");

            var response = await PostAsync(browser, "/Registration/Register", "/Registration/Register",
                new Dictionary<string, string> {["Email"] = "x@gmail.com", ["DisplayName"] = "X", ["Password"] = Password});

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("allowed domain", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task The_user_list_returns_display_names_for_the_conference_service()
        {
            var (factory, browser) = Start();
            GoogleIs("g-400", "lena@gmail.com", "Lena");
            var callback = await FollowAsync(browser, await browser.GetAsync("/External/Challenge"));
            await FollowAsync(browser, callback);
            await PostAsync(browser, "/External/Welcome", "/External/Welcome",
                new Dictionary<string, string> {["DisplayName"] = "Lena L"});
            var user = await factory.Services.GetRequiredService<IUserRepository>().FindByEmailAsync("LENA@GMAIL.COM");

            var list = await ListAsync(browser, new[] {user!.Id, "unknown-id"});

            Assert.Equal("Lena L", list![0].DisplayName);
            Assert.False(list[0].NotFound);
            Assert.True(list[1].NotFound);
        }

        private static async Task<List<UserDto>?> ListAsync(HttpClient browser, string[] ids)
        {
            var response = await browser.PostAsJsonAsync("/api/v1/user/list", ids);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<UserDto>>(
                new System.Text.Json.JsonSerializerOptions {PropertyNameCaseInsensitive = true});
        }

        private record UserDto(string Id, bool NotFound, string? DisplayName);

        // ---- demo mode

        [Fact]
        public async Task Demo_mode_has_no_registration_and_accepts_any_password()
        {
            var (_, browser) = Start(accounts: false);

            Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync("/Registration/Register")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync("/External/Challenge")).StatusCode);
            var page = await browser.GetStringAsync("/Account/Login");
            Assert.DoesNotContain("Continue with Google", page);
            Assert.Contains("demo environment", page);

            var response = await LoginAsync(browser, "demouser", "whatever");
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.True(HasSessionCookie(response));

            var list = await ListAsync(browser, new[] {Convert.ToHexString(System.Text.Encoding.ASCII.GetBytes("demouser"))});
            Assert.Equal("demouser", list!.Single().DisplayName);
        }
    }
}
