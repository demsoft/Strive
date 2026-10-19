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

        private void Check()
        {
            if (Unavailable) throw new PaymentProviderException("The provider is down.");
        }
    }
}
