using System;

namespace Strive.Core.Services.Payments
{
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

        public CreatorStatus Status { get; set; } = CreatorStatus.Active;

        public DateTimeOffset CreatedAt { get; init; }

        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>Whether the creator can be paid: a verified account and a sub account</summary>
        public bool CanSell => Status == CreatorStatus.Active && !string.IsNullOrEmpty(ProviderSubaccountCode);
    }
}
