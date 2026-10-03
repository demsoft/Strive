#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;

namespace Identity.API.Accounts
{
    public enum SignInStatus
    {
        Succeeded,
        InvalidCredentials,
        LockedOut,

        /// <summary>The password is right, but the email address has not been confirmed.</summary>
        EmailNotConfirmed,
    }

    public record SignInOutcome(SignInStatus Status, StriveUser? User = null);

    public record RegisterOutcome(bool Accepted, IReadOnlyList<string> Errors)
    {
        public static RegisterOutcome Ok => new(true, Array.Empty<string>());
        public static RegisterOutcome Fail(params string[] errors) => new(false, errors);
    }

    public enum ExternalSignInStatus
    {
        Succeeded,

        /// <summary>The provider does not vouch for the email address.</summary>
        EmailNotVerified,

        /// <summary>The domain of the email address may not create accounts.</summary>
        DomainNotAllowed,
    }

    public record ExternalSignInOutcome(ExternalSignInStatus Status, StriveUser? User = null, bool IsNew = false);

    /// <summary>
    ///     The rules of the accounts: registration, email confirmation, password sign in with lockout, password reset
    ///     and sign in with an external provider. No HTTP in here, so everything can be tested.
    /// </summary>
    public class AccountService
    {
        public const int MaxDisplayNameLength = 30;

        private readonly IEmailSender _email;
        private readonly ILogger<AccountService> _logger;
        private readonly AccountsOptions _options;
        private readonly PasswordHasher<StriveUser> _hasher = new();
        private readonly IUserRepository _users;
        private readonly Func<DateTimeOffset> _now;

        public AccountService(IUserRepository users, IEmailSender email, IOptions<AccountsOptions> options,
            ILogger<AccountService> logger, Func<DateTimeOffset>? now = null)
        {
            _users = users;
            _email = email;
            _options = options.Value;
            _logger = logger;
            _now = now ?? (() => DateTimeOffset.UtcNow);
        }

        public static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

        public async Task<RegisterOutcome> RegisterAsync(string? email, string? displayName, string? password,
            Func<string, string> confirmUrl, Func<string> signInUrl, Func<string> resetUrl)
        {
            var errors = new List<string>();
            email = email?.Trim();
            if (!IsValidEmail(email)) errors.Add("Enter a valid email address.");
            var nameError = ValidateDisplayName(displayName);
            if (nameError != null) errors.Add(nameError);
            errors.AddRange(ValidatePassword(password, email));
            if (errors.Count > 0) return new RegisterOutcome(false, errors);

            if (!_options.IsEmailDomainAllowed(email!))
                return RegisterOutcome.Fail("Accounts can only be created with an email address of an allowed domain.");

            var normalized = NormalizeEmail(email!);
            var user = new StriveUser
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Email = email!,
                NormalizedEmail = normalized,
                DisplayName = displayName!.Trim(),
                CreatedAt = _now(),
            };
            user.PasswordHash = _hasher.HashPassword(user, password!);

            if (await _users.TryInsertAsync(user))
            {
                await SendConfirmationAsync(user, confirmUrl);
            }
            else
            {
                // do not tell the visitor: the answer is the same for an unknown and for a known address
                var existing = await _users.FindByEmailAsync(normalized);
                if (existing != null)
                    await SafeSendAsync(EmailTemplates.AlreadyRegistered(existing.Email, signInUrl(), resetUrl()));
            }

            return RegisterOutcome.Ok;
        }

        public async Task SendConfirmationAsync(StriveUser user, Func<string, string> confirmUrl)
        {
            var token = await CreateTokenAsync(user, TokenPurpose.ConfirmEmail, _options.ConfirmationTokenLifetime);
            await SafeSendAsync(EmailTemplates.ConfirmEmail(user.Email, user.DisplayName ?? user.Email, confirmUrl(token)));
        }

        public async Task ResendConfirmationAsync(string? email, Func<string, string> confirmUrl)
        {
            if (string.IsNullOrWhiteSpace(email)) return;

            var user = await _users.FindByEmailAsync(NormalizeEmail(email));
            if (user is {EmailConfirmed: false, PasswordHash: not null}) await SendConfirmationAsync(user, confirmUrl);
        }

        public async Task<StriveUser?> ConfirmEmailAsync(string? token)
        {
            var user = await ConsumeAsync(token, TokenPurpose.ConfirmEmail);
            if (user == null) return null;

            user.EmailConfirmed = true;
            await _users.UpdateAsync(user);
            return user;
        }

        public async Task<SignInOutcome> PasswordSignInAsync(string? email, string? password)
        {
            var user = string.IsNullOrWhiteSpace(email) ? null : await _users.FindByEmailAsync(NormalizeEmail(email));
            if (user?.PasswordHash == null || string.IsNullOrEmpty(password))
            {
                // same work as for a real user, so that the response time does not reveal which emails exist
                _hasher.HashPassword(new StriveUser(), password ?? string.Empty);
                return new SignInOutcome(SignInStatus.InvalidCredentials);
            }

            var now = _now();
            if (user.LockoutEnd > now) return new SignInOutcome(SignInStatus.LockedOut);

            var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (result == PasswordVerificationResult.Failed)
            {
                user.AccessFailedCount++;
                if (user.AccessFailedCount >= _options.MaxFailedAttempts)
                {
                    user.LockoutEnd = now + _options.LockoutDuration;
                    user.AccessFailedCount = 0;
                    _logger.LogWarning("User {UserId} is locked out after too many failed sign ins", user.Id);
                }

                await _users.UpdateAsync(user);
                return new SignInOutcome(user.LockoutEnd > now ? SignInStatus.LockedOut : SignInStatus.InvalidCredentials);
            }

            if (result == PasswordVerificationResult.SuccessRehashNeeded)
                user.PasswordHash = _hasher.HashPassword(user, password);
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            await _users.UpdateAsync(user);

            return user.EmailConfirmed
                ? new SignInOutcome(SignInStatus.Succeeded, user)
                : new SignInOutcome(SignInStatus.EmailNotConfirmed, user);
        }

        public async Task ForgotPasswordAsync(string? email, Func<string, string> resetUrl)
        {
            if (string.IsNullOrWhiteSpace(email)) return;

            var user = await _users.FindByEmailAsync(NormalizeEmail(email));
            if (user == null) return;

            var token = await CreateTokenAsync(user, TokenPurpose.ResetPassword, _options.ResetTokenLifetime);
            await SafeSendAsync(EmailTemplates.ResetPassword(user.Email, resetUrl(token)));
        }

        /// <returns>The errors, empty when the password was changed.</returns>
        public async Task<IReadOnlyList<string>> ResetPasswordAsync(string? token, string? password)
        {
            // validate first: a typo in the password must not use up the link
            var errors = ValidatePassword(password, null);
            if (errors.Count > 0) return errors;

            var user = await ConsumeAsync(token, TokenPurpose.ResetPassword);
            if (user == null) return new[] {"This link is invalid or has expired. Ask for a new one."};

            user.PasswordHash = _hasher.HashPassword(user, password!);
            // the mail reached the owner of the address
            user.EmailConfirmed = true;
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            await _users.UpdateAsync(user);
            await _users.DeleteTokensAsync(user.Id, TokenPurpose.ResetPassword);
            return Array.Empty<string>();
        }

        /// <summary>Whether the link can still be used (to show the form or an error), without using it up.</summary>
        public async Task<bool> IsTokenValidAsync(string? token, TokenPurpose purpose)
        {
            return !string.IsNullOrEmpty(token) && await _users.PeekTokenAsync(HashToken(token), purpose) != null;
        }

        public async Task<ExternalSignInOutcome> ExternalSignInAsync(string provider, string key, string? email,
            bool emailVerified, string? name)
        {
            var linked = await _users.FindByLoginAsync(provider, key);
            if (linked != null) return new ExternalSignInOutcome(ExternalSignInStatus.Succeeded, linked);

            // without a verified email address anybody could claim the account of somebody else
            if (!emailVerified || !IsValidEmail(email))
                return new ExternalSignInOutcome(ExternalSignInStatus.EmailNotVerified);

            var normalized = NormalizeEmail(email!);
            var existing = await _users.FindByEmailAsync(normalized);
            if (existing != null)
            {
                if (!existing.EmailConfirmed)
                {
                    // somebody may have registered this address with a password of their own without owning it
                    existing.PasswordHash = null;
                    existing.EmailConfirmed = true;
                    await _users.DeleteTokensAsync(existing.Id, TokenPurpose.ConfirmEmail);
                    await _users.UpdateAsync(existing);
                }

                await _users.TryAddLoginAsync(provider, key, existing.Id);
                return new ExternalSignInOutcome(ExternalSignInStatus.Succeeded, existing);
            }

            if (!_options.IsEmailDomainAllowed(email!))
                return new ExternalSignInOutcome(ExternalSignInStatus.DomainNotAllowed);

            var user = new StriveUser
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Email = email!.Trim(),
                NormalizedEmail = normalized,
                SuggestedName = Truncate(name, MaxDisplayNameLength),
                EmailConfirmed = true,
                CreatedAt = _now(),
            };

            if (!await _users.TryInsertAsync(user))
            {
                // two sign ins at the same time: the other one won
                user = await _users.FindByEmailAsync(normalized) ?? throw new InvalidOperationException();
            }

            await _users.TryAddLoginAsync(provider, key, user.Id);
            return new ExternalSignInOutcome(ExternalSignInStatus.Succeeded, user, true);
        }

        public async Task<string?> SetDisplayNameAsync(string userId, string? displayName)
        {
            var error = ValidateDisplayName(displayName);
            if (error != null) return error;

            var user = await _users.FindByIdAsync(userId) ?? throw new InvalidOperationException("Unknown user.");
            user.DisplayName = displayName!.Trim();
            await _users.UpdateAsync(user);
            return null;
        }

        public Task<StriveUser?> FindByIdAsync(string id) => _users.FindByIdAsync(id);

        // ---- validation

        public static string? ValidateDisplayName(string? displayName)
        {
            var name = displayName?.Trim();
            if (string.IsNullOrEmpty(name)) return "Enter the name other participants should see.";
            if (name.Length > MaxDisplayNameLength) return $"The name can have at most {MaxDisplayNameLength} characters.";
            if (name.Any(char.IsControl)) return "The name contains characters that are not allowed.";
            // email addresses must not appear in meetings
            if (name.Contains('@')) return "Please do not use an email address as your name.";
            return null;
        }

        public IReadOnlyList<string> ValidatePassword(string? password, string? email)
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(password) || password.Length < _options.PasswordMinLength)
                errors.Add($"The password needs at least {_options.PasswordMinLength} characters.");
            else if (password.Length > 128)
                errors.Add("The password can have at most 128 characters.");
            else if (email != null && string.Equals(password, email, StringComparison.OrdinalIgnoreCase))
                errors.Add("The password must not be your email address.");
            return errors;
        }

        private static bool IsValidEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email) || email.Length > 254) return false;
            var at = email.IndexOf('@');
            return at > 0 && at == email.LastIndexOf('@') && email.IndexOf('.', at) > at + 1 &&
                   !email.EndsWith('.') && !email.Any(char.IsWhiteSpace);
        }

        // ---- tokens

        private async Task<string> CreateTokenAsync(StriveUser user, TokenPurpose purpose, TimeSpan lifetime)
        {
            // only the newest link works
            await _users.DeleteTokensAsync(user.Id, purpose);

            var token = Base64Url(RandomNumberGenerator.GetBytes(32));
            await _users.SaveTokenAsync(new UserToken
            {
                Id = HashToken(token), UserId = user.Id, Purpose = purpose, ExpiresAt = (_now() + lifetime).UtcDateTime,
            });
            return token;
        }

        private async Task<StriveUser?> ConsumeAsync(string? token, TokenPurpose purpose)
        {
            if (string.IsNullOrEmpty(token)) return null;

            var consumed = await _users.ConsumeTokenAsync(HashToken(token), purpose);
            return consumed == null ? null : await _users.FindByIdAsync(consumed.UserId);
        }

        private static string HashToken(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        private static string Base64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static string? Truncate(string? value, int length)
        {
            value = value?.Trim();
            return string.IsNullOrEmpty(value) ? null : value.Length <= length ? value : value[..length];
        }

        private async Task SafeSendAsync(EmailMessage message)
        {
            try
            {
                await _email.SendAsync(message);
            }
            catch (Exception e)
            {
                // the response must not depend on whether the mail could be sent to a known address
                _logger.LogError(e, "Could not send the email \"{Subject}\"", message.Subject);
            }
        }
    }
}
