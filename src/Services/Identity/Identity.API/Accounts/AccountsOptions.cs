#nullable enable
using System;
using System.Linq;

namespace Identity.API.Accounts
{
    public enum IdentityMode
    {
        /// <summary>Anyone can sign in with an alphanumeric name and any password. Development and the e2e tests.</summary>
        Demo,

        /// <summary>Real accounts (email and password, Google) stored in MongoDB.</summary>
        Accounts,
    }

    public class AccountsOptions
    {
        public const string Section = "Accounts";

        public IdentityMode Mode { get; set; } = IdentityMode.Demo;

        /// <summary>
        ///     Email domains that may create an account, separated by commas (for example "example.com, example.org").
        ///     Empty: everybody. Accounts that exist already keep working when a domain is removed from the list.
        /// </summary>
        public string AllowedEmailDomains { get; set; } = string.Empty;

        /// <summary>
        ///     Email addresses of the people that are server administrators (separated by commas): their sign in carries
        ///     the role "serveradmin", which opens the admin overview. Takes effect at the next sign in.
        /// </summary>
        public string AdminEmails { get; set; } = string.Empty;

        /// <summary>The shared key with which the API reads the account statistics (header X-Api-Key). Empty: off.</summary>
        public string? AdminApiKey { get; set; }

        public const string AdminRole = "serveradmin";

        public bool IsAdmin(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;

            return AdminEmails.Split(new[] {',', ';', ' '}, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(x => string.Equals(x, email.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public int PasswordMinLength { get; set; } = 8;

        /// <summary>
        ///     Whether a new password account must confirm its email address before it can sign in. When off, the
        ///     account can sign in at once (the address is still marked as unconfirmed).
        /// </summary>
        public bool RequireEmailConfirmation { get; set; } = true;
        public int MaxFailedAttempts { get; set; } = 5;
        public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);
        public TimeSpan ConfirmationTokenLifetime { get; set; } = TimeSpan.FromHours(24);
        public TimeSpan ResetTokenLifetime { get; set; } = TimeSpan.FromHours(1);

        public MongoOptions MongoDb { get; set; } = new();
        public GoogleOptions Google { get; set; } = new();
        public EmailOptions Email { get; set; } = new();

        public bool IsAccountsMode => Mode == IdentityMode.Accounts;

        public bool IsEmailDomainAllowed(string email)
        {
            var allowedDomains = AllowedEmailDomains
                .Split(new[] {',', ';', ' '}, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => x.TrimStart('@'))
                .ToList();
            if (allowedDomains.Count == 0) return true;

            var at = email.LastIndexOf('@');
            if (at < 0) return false;

            var domain = email[(at + 1)..];
            return allowedDomains.Any(x => string.Equals(x, domain, StringComparison.OrdinalIgnoreCase));
        }
    }

    public class MongoOptions
    {
        public string ConnectionString { get; set; } = "mongodb://localhost";
        public string DatabaseName { get; set; } = "strive-identity";
    }

    public class GoogleOptions
    {
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
    }

    public class EmailOptions
    {
        /// <summary>The sender of all emails, for example "Strive &lt;no-reply@example.com&gt;".</summary>
        public string From { get; set; } = "Strive <no-reply@localhost>";

        /// <summary>
        ///     Brevo (brevo.com) transactional API key. When set, mails are sent through the Brevo HTTP API (the sender
        ///     in From must be a validated sender or domain in Brevo) and the SMTP settings are not used.
        /// </summary>
        public string? BrevoApiKey { get; set; }

        public string? Host { get; set; }
        public int Port { get; set; } = 587;

        /// <summary>Use TLS (STARTTLS, or implicit TLS on port 465). Switched off for the local development inbox.</summary>
        public bool UseTls { get; set; } = true;

        public string? User { get; set; }
        public string? Password { get; set; }
    }
}
