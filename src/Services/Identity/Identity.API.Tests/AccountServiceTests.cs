using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Identity.API.Accounts;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Identity.API.Tests
{
    public class AccountServiceTests
    {
        private const string Password = "correct horse battery";

        private readonly RecordingEmailSender _email = new();
        private readonly InMemoryUserRepository _users = new();
        private readonly AccountsOptions _options = new() {Mode = IdentityMode.Accounts};
        private DateTimeOffset _now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        private AccountService Create()
        {
            _users.UtcNow = () => _now.UtcDateTime;
            return new AccountService(_users, _email, Options.Create(_options),
                NullLogger<AccountService>.Instance, () => _now);
        }

        private static string Token(string body) => Regex.Match(body, @"token=([\w-]+)").Groups[1].Value;

        private async Task<(AccountService service, StriveUser user)> RegisteredAndConfirmed(string email = "ann@example.com")
        {
            var service = Create();
            await Register(service, email);
            await service.ConfirmEmailAsync(Token(_email.Last.TextBody));
            return (service, (await _users.FindByEmailAsync(AccountService.NormalizeEmail(email)))!);
        }

        private static Task<RegisterOutcome> RegisterWith(AccountService service, string? email, string? name,
            string? password) => service.RegisterAsync(email, name, password,
            token => $"https://id/confirm?token={token}", () => "https://id/login", () => "https://id/forgot");

        private static Task<RegisterOutcome> Register(AccountService service, string email = "ann@example.com") =>
            RegisterWith(service, email, "Ann", Password);

        // ---- registration

        [Fact]
        public async Task Register_creates_an_unconfirmed_user_and_sends_a_confirmation_link()
        {
            var service = Create();

            var outcome = await Register(service);

            Assert.True(outcome.Accepted);
            var user = await _users.FindByEmailAsync("ANN@EXAMPLE.COM");
            Assert.NotNull(user);
            Assert.False(user!.EmailConfirmed);
            Assert.Equal("Ann", user.DisplayName);
            Assert.NotEqual(Password, user.PasswordHash);
            Assert.Equal("ann@example.com", _email.Last.To);
            Assert.Contains("token=", _email.Last.TextBody);
        }

        [Theory]
        [InlineData("", "Ann", Password)]
        [InlineData("not-an-email", "Ann", Password)]
        [InlineData("a@b", "Ann", Password)]
        [InlineData("ann@example.com", "", Password)]
        [InlineData("ann@example.com", "ann@example.com", Password)]
        [InlineData("ann@example.com", "Ann", "short")]
        [InlineData("ann@example.com", "Ann", "ann@example.com")]
        public async Task Register_rejects_invalid_input(string email, string name, string password)
        {
            var outcome = await RegisterWith(Create(), email, name, password);

            Assert.False(outcome.Accepted);
            Assert.NotEmpty(outcome.Errors);
            Assert.Empty(_email.Sent);
        }

        [Fact]
        public async Task Register_with_a_known_email_looks_the_same_but_mails_the_owner()
        {
            var service = Create();
            await Register(service);
            _email.Sent.Clear();

            var outcome = await RegisterWith(service, "ANN@example.com", "Somebody", "another long password");

            Assert.True(outcome.Accepted);
            Assert.Single(_email.Sent);
            Assert.Contains("already", _email.Last.Subject);
            // the existing account is untouched
            Assert.Equal("Ann", (await _users.FindByEmailAsync("ANN@EXAMPLE.COM"))!.DisplayName);
        }

        [Fact]
        public async Task Register_only_accepts_allowed_email_domains()
        {
            _options.AllowedEmailDomains = "Example.com";
            var service = Create();

            Assert.True((await Register(service, "ann@example.com")).Accepted);
            var rejected = await Register(service, "bob@gmail.com");

            Assert.False(rejected.Accepted);
            Assert.Null(await _users.FindByEmailAsync("BOB@GMAIL.COM"));
        }

        [Fact]
        public async Task A_failing_mail_server_does_not_fail_the_registration()
        {
            _email.Fail = true;

            Assert.True((await Register(Create())).Accepted);
        }

        [Fact]
        public async Task Without_required_confirmation_the_new_user_can_sign_in_at_once()
        {
            _options.RequireEmailConfirmation = false;
            var service = Create();

            var outcome = await Register(service);

            Assert.True(outcome.Accepted);
            Assert.NotNull(outcome.User);
            Assert.Empty(_email.Sent);
            Assert.Equal(SignInStatus.Succeeded, (await service.PasswordSignInAsync("ann@example.com", Password)).Status);
            // the address was not verified, a later Google sign in must not keep the password
            var google = await service.ExternalSignInAsync("Google", "g-5", "ann@example.com", true, "Ann");
            Assert.Null(google.User!.PasswordHash);
        }

        [Fact]
        public void Passwords_need_eight_characters_by_default()
        {
            var service = Create();

            Assert.NotEmpty(service.ValidatePassword("1234567", null));
            Assert.Empty(service.ValidatePassword("12345678", null));
        }

        // ---- confirmation

        [Fact]
        public async Task The_confirmation_link_works_once()
        {
            var service = Create();
            await Register(service);
            var token = Token(_email.Last.TextBody);

            Assert.True(await service.IsTokenValidAsync(token, TokenPurpose.ConfirmEmail));
            Assert.NotNull(await service.ConfirmEmailAsync(token));
            Assert.Null(await service.ConfirmEmailAsync(token));
            Assert.True((await _users.FindByEmailAsync("ANN@EXAMPLE.COM"))!.EmailConfirmed);
        }

        [Fact]
        public async Task The_confirmation_link_expires()
        {
            var service = Create();
            await Register(service);
            var token = Token(_email.Last.TextBody);

            _now += TimeSpan.FromHours(25);

            Assert.False(await service.IsTokenValidAsync(token, TokenPurpose.ConfirmEmail));
            Assert.Null(await service.ConfirmEmailAsync(token));
        }

        [Fact]
        public async Task A_confirmation_token_cannot_reset_a_password()
        {
            var service = Create();
            await Register(service);
            var token = Token(_email.Last.TextBody);

            var errors = await service.ResetPasswordAsync(token, "a brand new password");

            Assert.NotEmpty(errors);
        }

        [Fact]
        public async Task Only_the_newest_confirmation_link_works()
        {
            var service = Create();
            await Register(service);
            var first = Token(_email.Last.TextBody);

            await service.ResendConfirmationAsync("ann@example.com", t => $"x?token={t}");
            var second = Token(_email.Last.TextBody);

            Assert.Null(await service.ConfirmEmailAsync(first));
            Assert.NotNull(await service.ConfirmEmailAsync(second));
        }

        // ---- password sign in

        [Fact]
        public async Task Sign_in_needs_the_right_password_and_a_confirmed_email()
        {
            var service = Create();
            await Register(service);

            Assert.Equal(SignInStatus.EmailNotConfirmed, (await service.PasswordSignInAsync("ann@example.com", Password)).Status);
            await service.ConfirmEmailAsync(Token(_email.Last.TextBody));

            Assert.Equal(SignInStatus.InvalidCredentials, (await service.PasswordSignInAsync("ann@example.com", "wrong")).Status);
            var ok = await service.PasswordSignInAsync(" ANN@example.com ", Password);
            Assert.Equal(SignInStatus.Succeeded, ok.Status);
            Assert.Equal("Ann", ok.User!.DisplayName);
        }

        [Fact]
        public async Task An_unknown_email_is_just_invalid_credentials()
        {
            var outcome = await Create().PasswordSignInAsync("nobody@example.com", Password);

            Assert.Equal(SignInStatus.InvalidCredentials, outcome.Status);
        }

        [Fact]
        public async Task Too_many_wrong_passwords_lock_the_account_for_a_while()
        {
            var (service, _) = await RegisteredAndConfirmed();

            for (var i = 0; i < 4; i++)
                Assert.Equal(SignInStatus.InvalidCredentials, (await service.PasswordSignInAsync("ann@example.com", "wrong")).Status);
            Assert.Equal(SignInStatus.LockedOut, (await service.PasswordSignInAsync("ann@example.com", "wrong")).Status);

            // even the right password does not help while locked
            Assert.Equal(SignInStatus.LockedOut, (await service.PasswordSignInAsync("ann@example.com", Password)).Status);

            _now += TimeSpan.FromMinutes(16);
            Assert.Equal(SignInStatus.Succeeded, (await service.PasswordSignInAsync("ann@example.com", Password)).Status);
        }

        [Fact]
        public async Task A_successful_sign_in_resets_the_failure_count()
        {
            var (service, _) = await RegisteredAndConfirmed();

            for (var round = 0; round < 3; round++)
            {
                for (var i = 0; i < 4; i++) await service.PasswordSignInAsync("ann@example.com", "wrong");
                Assert.Equal(SignInStatus.Succeeded, (await service.PasswordSignInAsync("ann@example.com", Password)).Status);
            }
        }

        // ---- password reset

        [Fact]
        public async Task Forgot_password_mails_a_link_only_to_known_addresses_and_says_nothing_else()
        {
            var (service, _) = await RegisteredAndConfirmed();
            _email.Sent.Clear();

            await service.ForgotPasswordAsync("nobody@example.com", t => $"x?token={t}");
            Assert.Empty(_email.Sent);

            await service.ForgotPasswordAsync("ann@example.com", t => $"x?token={t}");
            Assert.Single(_email.Sent);
        }

        [Fact]
        public async Task Reset_changes_the_password_once_and_clears_a_lockout()
        {
            var (service, _) = await RegisteredAndConfirmed();
            for (var i = 0; i < 5; i++) await service.PasswordSignInAsync("ann@example.com", "wrong");
            await service.ForgotPasswordAsync("ann@example.com", t => $"x?token={t}");
            var token = Token(_email.Last.TextBody);

            Assert.Empty(await service.ResetPasswordAsync(token, "my new long password"));

            Assert.Equal(SignInStatus.Succeeded, (await service.PasswordSignInAsync("ann@example.com", "my new long password")).Status);
            Assert.Equal(SignInStatus.InvalidCredentials, (await service.PasswordSignInAsync("ann@example.com", Password)).Status);
            Assert.NotEmpty(await service.ResetPasswordAsync(token, "yet another long password"));
        }

        [Fact]
        public async Task A_weak_new_password_does_not_use_up_the_reset_link()
        {
            var (service, _) = await RegisteredAndConfirmed();
            await service.ForgotPasswordAsync("ann@example.com", t => $"x?token={t}");
            var token = Token(_email.Last.TextBody);

            Assert.NotEmpty(await service.ResetPasswordAsync(token, "short"));

            Assert.Empty(await service.ResetPasswordAsync(token, "a long enough password"));
        }

        [Fact]
        public async Task The_reset_link_expires_after_an_hour()
        {
            var (service, _) = await RegisteredAndConfirmed();
            await service.ForgotPasswordAsync("ann@example.com", t => $"x?token={t}");
            var token = Token(_email.Last.TextBody);

            _now += TimeSpan.FromMinutes(61);

            Assert.NotEmpty(await service.ResetPasswordAsync(token, "a long enough password"));
        }

        [Fact]
        public async Task Resetting_the_password_confirms_the_email_address()
        {
            var service = Create();
            await Register(service);
            await service.ForgotPasswordAsync("ann@example.com", t => $"x?token={t}");

            await service.ResetPasswordAsync(Token(_email.Last.TextBody), "a long enough password");

            Assert.Equal(SignInStatus.Succeeded, (await service.PasswordSignInAsync("ann@example.com", "a long enough password")).Status);
        }

        // ---- external sign in

        [Fact]
        public async Task A_first_google_sign_in_creates_a_confirmed_user_without_a_display_name()
        {
            var service = Create();

            var outcome = await service.ExternalSignInAsync("Google", "g-1", "gina@gmail.com", true, "Gina Gee");

            Assert.Equal(ExternalSignInStatus.Succeeded, outcome.Status);
            Assert.True(outcome.IsNew);
            Assert.True(outcome.User!.EmailConfirmed);
            Assert.False(outcome.User.HasDisplayName);
            Assert.Equal("Gina Gee", outcome.User.SuggestedName);
            Assert.Null(outcome.User.PasswordHash);
        }

        [Fact]
        public async Task The_same_google_account_signs_in_as_the_same_user()
        {
            var service = Create();
            var first = await service.ExternalSignInAsync("Google", "g-1", "gina@gmail.com", true, "Gina");
            await service.SetDisplayNameAsync(first.User!.Id, "Gigi");

            // even when the email address changed at Google
            var second = await service.ExternalSignInAsync("google", "g-1", "gina.new@gmail.com", true, "Gina");

            Assert.False(second.IsNew);
            Assert.Equal(first.User.Id, second.User!.Id);
            Assert.Equal("Gigi", second.User.DisplayName);
        }

        [Fact]
        public async Task Google_links_to_the_account_with_the_same_verified_email()
        {
            var (service, user) = await RegisteredAndConfirmed("ann@example.com");

            var outcome = await service.ExternalSignInAsync("Google", "g-9", "Ann@Example.com", true, "Ann G");

            Assert.Equal(user.Id, outcome.User!.Id);
            Assert.False(outcome.IsNew);
            Assert.Equal("Ann", outcome.User.DisplayName);
            // the password keeps working
            Assert.Equal(SignInStatus.Succeeded, (await service.PasswordSignInAsync("ann@example.com", Password)).Status);
        }

        [Fact]
        public async Task Google_taking_over_an_unconfirmed_registration_drops_the_password_of_the_squatter()
        {
            var service = Create();
            await Register(service, "victim@example.com");

            var outcome = await service.ExternalSignInAsync("Google", "g-7", "victim@example.com", true, "Victim");

            Assert.True(outcome.User!.EmailConfirmed);
            Assert.Null(outcome.User.PasswordHash);
            Assert.Equal(SignInStatus.InvalidCredentials, (await service.PasswordSignInAsync("victim@example.com", Password)).Status);
            // and the old confirmation link is dead
            Assert.Null(await service.ConfirmEmailAsync(Token(_email.Last.TextBody)));
        }

        [Fact]
        public async Task An_unverified_google_email_is_never_trusted()
        {
            var (service, _) = await RegisteredAndConfirmed("ann@example.com");

            var outcome = await service.ExternalSignInAsync("Google", "g-evil", "ann@example.com", false, "Evil");

            Assert.Equal(ExternalSignInStatus.EmailNotVerified, outcome.Status);
            Assert.Null(outcome.User);
        }

        [Fact]
        public async Task Google_sign_up_is_limited_to_the_allowed_domains_but_existing_users_stay_in()
        {
            _options.AllowedEmailDomains = "example.com";
            var service = Create();

            var rejected = await service.ExternalSignInAsync("Google", "g-1", "gina@gmail.com", true, "Gina");
            Assert.Equal(ExternalSignInStatus.DomainNotAllowed, rejected.Status);
            Assert.Null(await _users.FindByEmailAsync("GINA@GMAIL.COM"));

            var allowed = await service.ExternalSignInAsync("Google", "g-2", "ann@example.com", true, "Ann");
            Assert.Equal(ExternalSignInStatus.Succeeded, allowed.Status);

            // the domain is removed later: the person has an account, so they can still sign in
            _options.AllowedEmailDomains = "other.org, example.net";
            Assert.Equal(ExternalSignInStatus.Succeeded,
                (await service.ExternalSignInAsync("Google", "g-2", "ann@example.com", true, "Ann")).Status);
        }

        // ---- display name

        [Theory]
        [InlineData("Ann", true)]
        [InlineData("  Ann Lee  ", true)]
        [InlineData("Zoë 😀", true)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData("ann@example.com", false)]
        [InlineData("bad\u0007name", false)]
        public void Display_names_are_validated(string name, bool valid)
        {
            Assert.Equal(valid, AccountService.ValidateDisplayName(name) == null);
        }

        [Fact]
        public void A_display_name_is_limited_in_length()
        {
            Assert.NotNull(AccountService.ValidateDisplayName(new string('a', 31)));
            Assert.Null(AccountService.ValidateDisplayName(new string('a', 30)));
        }

        [Fact]
        public async Task The_display_name_is_stored_trimmed()
        {
            var service = Create();
            var user = (await service.ExternalSignInAsync("Google", "g-1", "gina@gmail.com", true, "Gina")).User!;

            Assert.Null(await service.SetDisplayNameAsync(user.Id, "  Gigi "));

            Assert.Equal("Gigi", (await _users.FindByIdAsync(user.Id))!.DisplayName);
        }

        [Fact]
        public async Task Different_users_get_different_ids_that_cannot_be_demo_ids()
        {
            var service = Create();
            var a = (await service.ExternalSignInAsync("Google", "1", "a@x.com", true, "A")).User!;
            var b = (await service.ExternalSignInAsync("Google", "2", "b@x.com", true, "B")).User!;

            Assert.NotEqual(a.Id, b.Id);
            // a demo id is the hex encoding of a username shorter than 12 characters: at most 22 characters
            Assert.Equal(24, a.Id.Length);
        }
    }
}
