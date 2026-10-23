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

        /// <summary>Starts a payment: the person is sent to the returned address to pay</summary>
        Task<CheckoutSession> InitializeCheckout(CheckoutRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Asks the provider what happened to a payment. Only this answer proves a payment, never what the browser says.
        ///     Throws <see cref="TransactionNotFoundException" /> if the provider does not know the reference.
        /// </summary>
        Task<VerifiedTransaction> VerifyTransaction(string reference, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Charges the card of an earlier payment again (the person is not there). The answer of the provider is
        ///     not trusted: the caller verifies the payment afterwards like any other. Throws
        ///     <see cref="PaymentProviderException" /> if the provider did not take the request.
        /// </summary>
        Task ChargeAuthorization(ChargeAuthorizationRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gives the whole payment back to the buyer. Throws <see cref="PaymentProviderException" /> if the provider
        ///     did not accept the refund (the caller tries again later).
        /// </summary>
        Task<RefundResult> RefundTransaction(string reference, long amountMinor,
            CancellationToken cancellationToken = default);

        /// <summary>Whether a notification really comes from the provider (a signature over the exact bytes)</summary>
        bool IsValidWebhookSignature(byte[] rawBody, string? signature);
    }

    /// <param name="PlatformAmountMinor">The part of the amount that stays with the platform</param>
    /// <param name="SubaccountCode">The sub account of the creator that receives the rest</param>
    public record CheckoutRequest(string Reference, string Email, long AmountMinor, string Currency, string CallbackUrl,
        string SubaccountCode, long PlatformAmountMinor, IReadOnlyDictionary<string, string> Metadata);

    /// <param name="AuthorizationCode">The token of the card, from the first payment</param>
    public record ChargeAuthorizationRequest(string Reference, string Email, string AuthorizationCode,
        long AmountMinor, string Currency, string SubaccountCode, long PlatformAmountMinor,
        IReadOnlyDictionary<string, string> Metadata);

    public record CheckoutSession(string AuthorizationUrl, string AccessCode);

    /// <param name="Paid">The provider confirms the money was received</param>
    /// <param name="Failed">The provider says it did not work (not just: not paid yet)</param>
    /// <param name="AuthorizationCode">The token of the card, if the card can be charged again (reusable)</param>
    public record VerifiedTransaction(string Reference, bool Paid, bool Failed, long AmountMinor, string Currency,
        string? Channel, string? ProviderTransactionId, DateTimeOffset? PaidAt, string? AuthorizationCode = null);

    /// <param name="RefundId">The id at the provider, if it gave one</param>
    /// <param name="Status">What the provider says about the refund (pending, processing, processed)</param>
    public record RefundResult(string? RefundId, string Status);

    public class TransactionNotFoundException : Exception
    {
        public TransactionNotFoundException(string message) : base(message)
        {
        }
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
