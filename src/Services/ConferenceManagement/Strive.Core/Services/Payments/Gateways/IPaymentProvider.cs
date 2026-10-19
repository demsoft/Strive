using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Strive.Core.Services.Payments.Gateways
{
    /// <summary>
    ///     The payment provider (Paystack today). The rest of the application only knows this interface, so that another
    ///     provider can be used for another country.
    /// </summary>
    public interface IPaymentProvider
    {
        Task<IReadOnlyList<ProviderBank>> ListBanks(string country, CancellationToken cancellationToken = default);

        /// <summary>Looks up the holder of a bank account. Throws <see cref="InvalidAccountException" /> if there is none.</summary>
        Task<ResolvedAccount> ResolveAccount(string bankCode, string accountNumber,
            CancellationToken cancellationToken = default);

        /// <summary>Creates the sub account that receives the share of a creator. Returns its code.</summary>
        Task<string> CreateSubaccount(SubaccountRequest request, CancellationToken cancellationToken = default);

        Task UpdateSubaccount(string subaccountCode, SubaccountRequest request,
            CancellationToken cancellationToken = default);
    }

    public record ProviderBank(string Code, string Name);

    public record ResolvedAccount(string AccountName, string AccountNumber);

    /// <param name="BusinessName">The name shown to the provider and on statements</param>
    /// <param name="PlatformPercentage">The share of a payment that stays with the platform</param>
    public record SubaccountRequest(string BusinessName, string BankCode, string AccountNumber,
        decimal PlatformPercentage);

    /// <summary>The bank account does not exist or does not match the bank</summary>
    public class InvalidAccountException : Exception
    {
        public InvalidAccountException(string message) : base(message)
        {
        }
    }

    /// <summary>The provider did not answer, refused the request or is not set up</summary>
    public class PaymentProviderException : Exception
    {
        public PaymentProviderException(string message, Exception? inner = null) : base(message, inner)
        {
        }
    }
}
