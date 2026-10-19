using System.Collections.Generic;
using MediatR;
using Strive.Core.Interfaces;
using Strive.Core.Services.Payments.Gateways;

namespace Strive.Core.Services.Payments.Requests
{
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
