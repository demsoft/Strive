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

        public int PasswordMinLength { get; set; } = 10;
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

        public string? Host { get; set; }
        public int Port { get; set; } = 587;

        /// <summary>Use TLS (STARTTLS, or implicit TLS on port 465). Switched off for the local development inbox.</summary>
        public bool UseTls { get; set; } = true;

        public string? User { get; set; }
        public string? Password { get; set; }
    }
}
