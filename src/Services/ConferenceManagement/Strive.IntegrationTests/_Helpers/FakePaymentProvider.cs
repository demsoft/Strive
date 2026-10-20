using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Strive.Core.Services.Payments.Gateways;

namespace Strive.IntegrationTests._Helpers
{
    /// <summary>
    ///     Stands in for the payment provider. The account number 0000000000 does not exist, every other number belongs
    ///     to "ADA LOVELACE"; it remembers what it was asked to do.
    /// </summary>
    public class FakePaymentProvider : IPaymentProvider
    {
        public const string UnknownAccount = "0000000000";

        public ConcurrentQueue<SubaccountRequest> CreatedSubaccounts { get; } = new();

        public ConcurrentQueue<(string Code, SubaccountRequest Request)> UpdatedSubaccounts { get; } = new();

        public bool Unavailable { get; set; }

        public ConcurrentQueue<CheckoutRequest> Checkouts { get; } = new();

        /// <summary>What the provider says about a payment, by reference. Unknown: the person has not paid.</summary>
        public ConcurrentDictionary<string, VerifiedTransaction> Transactions { get; } = new();

        /// <summary>The key the notifications are signed with (the same as the key in the test settings)</summary>
        public const string SecretKey = "sk_test_integration";

        /// <summary>The person pays: the provider will confirm it</summary>
        public void Pay(string reference, long amountMinor, string currency = "NGN") =>
            Transactions[reference] = new VerifiedTransaction(reference, true, false, amountMinor, currency, "card",
                "4099260516", DateTimeOffset.UtcNow);

        public void Fail(string reference, long amountMinor) =>
            Transactions[reference] = new VerifiedTransaction(reference, false, true, amountMinor, "NGN", "card", null,
                null);

        public static string Sign(byte[] body)
        {
            using var hmac = new System.Security.Cryptography.HMACSHA512(System.Text.Encoding.UTF8.GetBytes(SecretKey));
            return Convert.ToHexString(hmac.ComputeHash(body)).ToLowerInvariant();
        }

        private int _counter;

        public Task<IReadOnlyList<ProviderBank>> ListBanks(string country, CancellationToken cancellationToken = default)
        {
            Check();
            return Task.FromResult<IReadOnlyList<ProviderBank>>(new[]
            {
                new ProviderBank("058", "Guaranty Trust Bank"), new ProviderBank("044", "Access Bank"),
            });
        }

        public Task<ResolvedAccount> ResolveAccount(string bankCode, string accountNumber,
            CancellationToken cancellationToken = default)
        {
            Check();
            if (accountNumber == UnknownAccount) throw new InvalidAccountException("Could not resolve account name.");
            return Task.FromResult(new ResolvedAccount("ADA LOVELACE", accountNumber));
        }

        public Task<string> CreateSubaccount(SubaccountRequest request, CancellationToken cancellationToken = default)
        {
            Check();
            CreatedSubaccounts.Enqueue(request);
            return Task.FromResult($"ACCT_fake{Interlocked.Increment(ref _counter)}");
        }

        public Task UpdateSubaccount(string subaccountCode, SubaccountRequest request,
            CancellationToken cancellationToken = default)
        {
            Check();
            UpdatedSubaccounts.Enqueue((subaccountCode, request));
            return Task.CompletedTask;
        }

        public Task<CheckoutSession> InitializeCheckout(CheckoutRequest request,
            CancellationToken cancellationToken = default)
        {
            Check();
            Checkouts.Enqueue(request);
            return Task.FromResult(new CheckoutSession($"https://checkout.test/{request.Reference}", "access"));
        }

        public Task<VerifiedTransaction> VerifyTransaction(string reference,
            CancellationToken cancellationToken = default)
        {
            Check();
            if (!Transactions.TryGetValue(reference, out var transaction))
                throw new TransactionNotFoundException("Transaction reference not found.");
            return Task.FromResult(transaction);
        }

        /// <summary>The refunds the provider was asked for, as (reference, amount)</summary>
        public ConcurrentQueue<(string Reference, long AmountMinor)> Refunds { get; } = new();

        /// <summary>Refunds fail while this is set (the provider is up, but refuses)</summary>
        public bool RefuseRefunds { get; set; }

        public Task<RefundResult> RefundTransaction(string reference, long amountMinor,
            CancellationToken cancellationToken = default)
        {
            Check();
            if (RefuseRefunds) throw new PaymentProviderException("Insufficient balance.");
            Refunds.Enqueue((reference, amountMinor));
            return Task.FromResult(new RefundResult("rf_" + reference, "pending"));
        }

        public bool IsValidWebhookSignature(byte[] rawBody, string? signature) => signature == Sign(rawBody);

        private void Check()
        {
            if (Unavailable) throw new PaymentProviderException("The provider is down.");
        }
    }
}
