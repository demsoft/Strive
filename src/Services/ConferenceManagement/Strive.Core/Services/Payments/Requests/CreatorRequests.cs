using System.Collections.Generic;
using MediatR;
using Strive.Core.Interfaces;
using Strive.Core.Services.Payments.Gateways;

namespace Strive.Core.Services.Payments.Requests
{
    /// <summary>A creator asks to publish football matches (Arena); an administrator decides</summary>
    public record RequestOrganizerRequest(string UserId, string OrganizationName, string About)
        : IRequest<SuccessOrError<CreatorProfile>>;

    /// <summary>An administrator approves or rejects an organizer (also takes back an approval)</summary>
    public record DecideOrganizerRequest(string CreatorId, bool Approve, string? Note)
        : IRequest<SuccessOrError<CreatorProfile>>;

    public record ListBanksRequest : IRequest<SuccessOrError<IReadOnlyList<ProviderBank>>>;

    /// <summary>Looks up who owns a bank account, so that the creator can check it before saving</summary>
    public record ResolveBankAccountRequest(string BankCode, string AccountNumber)
        : IRequest<SuccessOrError<ResolvedAccount>>;

    /// <summary>
    ///     Creates the profile of a creator or changes it. The bank account is verified and the sub account at the
    ///     provider is created or updated. The account number is only needed when it is new or changed.
    /// </summary>
    public record SaveCreatorProfileRequest(string UserId, string DisplayName, string? Bio, string? BankCode,
        string? AccountNumber) : IRequest<SuccessOrError<CreatorProfile>>;
}
