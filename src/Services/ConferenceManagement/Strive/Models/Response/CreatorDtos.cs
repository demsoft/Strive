using System;
using Strive.Core.Services.Payments;

namespace Strive.Models.Response
{
    /// <summary>What the creator page needs to know about payments on this server</summary>
    public record PaymentsConfigDto(bool Enabled, string Country, string Currency, int PlatformCommissionPercent);

    /// <summary>A creator as the creator sees themself</summary>
    public record CreatorProfileDto(string CreatorId, string DisplayName, string? Bio, string Country, string Currency,
        string? BankCode, string? BankName, string? AccountLast4, string? AccountName, bool CanSell,
        DateTimeOffset CreatedAt)
    {
        public static CreatorProfileDto From(CreatorProfile profile)
        {
            return new CreatorProfileDto(profile.CreatorId, profile.DisplayName, profile.Bio, profile.Country,
                profile.Currency, profile.BankCode, profile.BankName, profile.AccountLast4, profile.AccountName,
                profile.CanSell, profile.CreatedAt);
        }
    }

    /// <summary>What everybody may see of a creator: no bank details</summary>
    public record PublicCreatorDto(string CreatorId, string DisplayName, string? Bio)
    {
        public static PublicCreatorDto From(CreatorProfile profile)
        {
            return new PublicCreatorDto(profile.CreatorId, profile.DisplayName, profile.Bio);
        }
    }

    public record BankDto(string Code, string Name);

    public record ResolveAccountDto(string BankCode, string AccountNumber);

    public record ResolvedAccountDto(string AccountName);

    public record SaveCreatorDto(string DisplayName, string? Bio, string? BankCode, string? AccountNumber);
}
