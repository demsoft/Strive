using System;

namespace Strive.Core.Services.Payments
{
    public static class PassRules
    {
        /// <summary>How long a pass lasts after it was paid (buying another one while it runs adds to it)</summary>
        public const int Days = 30;
    }

    public static class SlugRules
    {
        public const int MinLength = 3;
        public const int MaxLength = 30;

        private static readonly System.Text.RegularExpressions.Regex Shape =
            new("^[a-z0-9]+(-[a-z0-9]+)*$", System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>Names that look like the platform or its staff, or that would confuse the routes</summary>
        private static readonly System.Collections.Generic.HashSet<string> Reserved =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "admin", "administrator", "support", "help", "official", "gbedu", "strive", "arena", "headboy", "api",
                "www", "me", "creator", "creators", "tickets", "ticket", "recordings", "login", "logout", "signup",
                "settings", "security", "billing", "payments", "paystack", "root", "system", "staff", "team", "null",
            };

        /// <summary>Capitals and spaces around it are forgiven: " Ada " becomes "ada"</summary>
        public static string Normalize(string? input) => (input ?? "").Trim().ToLowerInvariant();

        /// <returns>Null if the slug is fine, otherwise what is wrong with it</returns>
        public static string? Problem(string slug)
        {
            if (slug.Length is < MinLength or > MaxLength)
                return $"The name must have {MinLength} to {MaxLength} characters.";
            if (!Shape.IsMatch(slug))
                return "Use only letters, numbers and single hyphens (no hyphen at the start or the end).";
            if (Reserved.Contains(slug)) return "This name is reserved. Choose another one.";
            return null;
        }
    }

    public enum CreatorStatus
    {
        /// <summary>Verified bank account, can sell tickets</summary>
        Active,

        /// <summary>Not allowed to sell tickets (decided by an admin)</summary>
        Suspended,
    }

    /// <summary>
    ///     A person who hosts paid sessions. The bank account is verified with the payment provider, which keeps the full
    ///     number: only the last four digits are kept here.
    /// </summary>
    public class CreatorProfile
    {
        public CreatorProfile(string creatorId, string userId, string displayName, DateTimeOffset createdAt)
        {
            CreatorId = creatorId;
            UserId = userId;
            DisplayName = displayName;
            CreatedAt = createdAt;
            UpdatedAt = createdAt;
        }

        public string CreatorId { get; init; }

        /// <summary>The account of the person (the subject of the sign in token)</summary>
        public string UserId { get; init; }

        public string DisplayName { get; set; }

        public string? Bio { get; set; }

        public string Country { get; set; } = "NG";

        public string Currency { get; set; } = "NGN";

        public string? BankCode { get; set; }

        public string? BankName { get; set; }

        public string? AccountLast4 { get; set; }

        /// <summary>The name of the account holder as the bank knows it</summary>
        public string? AccountName { get; set; }

        /// <summary>The sub account at the payment provider that receives the share of the creator</summary>
        public string? ProviderSubaccountCode { get; set; }

        /// <summary>
        ///     What a pass costs in minor units: it gives access to every session of this creator for
        ///     <see cref="PassRules.Days" /> days. Null: the creator does not sell a pass.
        /// </summary>
        public long? PassPriceMinor { get; set; }

        /// <summary>
        ///     The short name in the link of the page of the creator (/u/ada), unique, see <see cref="SlugRules" />.
        ///     Null: the link uses the id.
        /// </summary>
        public string? Slug { get; set; }

        public CreatorStatus Status { get; set; } = CreatorStatus.Active;

        public DateTimeOffset CreatedAt { get; init; }

        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>Whether the creator can be paid: a verified account and a sub account</summary>
        public bool CanSell => Status == CreatorStatus.Active && !string.IsNullOrEmpty(ProviderSubaccountCode);
    }
}
