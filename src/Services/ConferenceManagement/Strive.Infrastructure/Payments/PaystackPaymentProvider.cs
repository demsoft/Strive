using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Strive.Core.Services.Payments;
using Strive.Core.Services.Payments.Gateways;

namespace Strive.Infrastructure.Payments
{
    /// <summary>
    ///     Paystack (https://paystack.com/docs/api). The secret key is sent in the Authorization header and never logged.
    /// </summary>
    public class PaystackPaymentProvider : IPaymentProvider
    {
        private const int BanksPerPage = 100;
        private const int MaxBankPages = 10;
        private static readonly TimeSpan BankCacheTime = TimeSpan.FromHours(1);

        private static readonly Dictionary<string, string> CountryNames = new(StringComparer.OrdinalIgnoreCase)
        {
            {"NG", "nigeria"}, {"GH", "ghana"}, {"KE", "kenya"}, {"ZA", "south africa"},
        };

        private readonly HttpClient _client;
        private readonly PaystackOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly BankListCache _cache;

        public PaystackPaymentProvider(HttpClient client, IOptions<PaymentsOptions> options, TimeProvider timeProvider,
            BankListCache cache)
        {
            _cache = cache;
            _client = client;
            _options = options.Value.Paystack;
            _timeProvider = timeProvider;
        }

        public async Task<IReadOnlyList<ProviderBank>> ListBanks(string country,
            CancellationToken cancellationToken = default)
        {
            if (_cache.TryGet(country, _timeProvider.GetUtcNow(), BankCacheTime, out var cached)) return cached;

            var name = CountryNames.TryGetValue(country, out var n) ? n : country.ToLowerInvariant();
            var banks = new Dictionary<string, ProviderBank>();
            for (var page = 1; page <= MaxBankPages; page++)
            {
                var url = $"bank?country={Uri.EscapeDataString(name)}&perPage={BanksPerPage}&page={page}";
                var response = await Send<List<BankDto>>(HttpMethod.Get, url, null, cancellationToken);
                var data = response.Data ?? new List<BankDto>();
                foreach (var bank in data.Where(x => !string.IsNullOrEmpty(x.Code) && !string.IsNullOrEmpty(x.Name)))
                    banks[bank.Code!] = new ProviderBank(bank.Code!, bank.Name!);
                if (data.Count < BanksPerPage) break;
            }

            var list = banks.Values.ToList();
            _cache.Set(country, _timeProvider.GetUtcNow(), list);

            return list;
        }

        public async Task<ResolvedAccount> ResolveAccount(string bankCode, string accountNumber,
            CancellationToken cancellationToken = default)
        {
            var url = $"bank/resolve?account_number={Uri.EscapeDataString(accountNumber)}&bank_code={Uri.EscapeDataString(bankCode)}";
            try
            {
                var response = await Send<ResolveDto>(HttpMethod.Get, url, null, cancellationToken);
                if (string.IsNullOrWhiteSpace(response.Data?.AccountName)) throw new InvalidAccountException("No account name.");
                return new ResolvedAccount(response.Data.AccountName!, response.Data.AccountNumber ?? accountNumber);
            }
            catch (ProviderRefusedException e) when (e.StatusCode is HttpStatusCode.UnprocessableEntity
                                                         or HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
            {
                // a number that does not exist is answered with 422: the person made a mistake, the service is fine
                throw new InvalidAccountException(e.Message);
            }
        }

        public async Task<string> CreateSubaccount(SubaccountRequest request,
            CancellationToken cancellationToken = default)
        {
            var response = await SendSubaccount<SubaccountDto>(HttpMethod.Post, "subaccount", request, cancellationToken);
            if (string.IsNullOrEmpty(response.Data?.SubaccountCode))
                throw new PaymentProviderException("Paystack answered without a sub account code.");
            return response.Data.SubaccountCode!;
        }

        public async Task UpdateSubaccount(string subaccountCode, SubaccountRequest request,
            CancellationToken cancellationToken = default)
        {
            await SendSubaccount<SubaccountDto>(HttpMethod.Put, $"subaccount/{Uri.EscapeDataString(subaccountCode)}",
                request, cancellationToken);
        }

        public async Task<CheckoutSession> InitializeCheckout(CheckoutRequest request,
            CancellationToken cancellationToken = default)
        {
            var body = new
            {
                email = request.Email,
                amount = request.AmountMinor,
                currency = request.Currency,
                reference = request.Reference,
                callback_url = request.CallbackUrl,
                subaccount = request.SubaccountCode,
                // what stays with the platform, in minor units; the rest goes to the sub account of the creator
                transaction_charge = request.PlatformAmountMinor,
                // the platform pays the fees of the payment
                bearer = "account",
                metadata = request.Metadata,
            };

            var response = await Send<InitializeDto>(HttpMethod.Post, "transaction/initialize", JsonContent.Create(body),
                cancellationToken);
            if (string.IsNullOrEmpty(response.Data?.AuthorizationUrl))
                throw new PaymentProviderException("Paystack answered without a payment address.");
            return new CheckoutSession(response.Data.AuthorizationUrl!, response.Data.AccessCode ?? "");
        }

        public async Task<VerifiedTransaction> VerifyTransaction(string reference,
            CancellationToken cancellationToken = default)
        {
            Envelope<TransactionDto> response;
            try
            {
                response = await Send<TransactionDto>(HttpMethod.Get,
                    $"transaction/verify/{Uri.EscapeDataString(reference)}", null, cancellationToken);
            }
            catch (ProviderRefusedException e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
                throw new TransactionNotFoundException(e.Message);
            }

            var data = response.Data ?? throw new PaymentProviderException("Paystack answered without data.");
            var status = data.Status ?? "";
            return new VerifiedTransaction(data.Reference ?? reference,
                status == "success",
                status is "failed" or "reversed",
                data.Amount, data.Currency ?? "", data.Channel,
                data.Id == 0 ? null : data.Id.ToString(CultureInfo.InvariantCulture),
                data.PaidAt);
        }

        public bool IsValidWebhookSignature(byte[] rawBody, string? signature)
        {
            if (string.IsNullOrWhiteSpace(_options.SecretKey) || string.IsNullOrWhiteSpace(signature)) return false;

            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_options.SecretKey));
            var expected = hmac.ComputeHash(rawBody);

            byte[] supplied;
            try
            {
                supplied = Convert.FromHexString(signature.Trim());
            }
            catch (FormatException)
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(expected, supplied);
        }

        private async Task<Envelope<T>> SendSubaccount<T>(HttpMethod method, string url, SubaccountRequest request,
            CancellationToken cancellationToken)
        {
            var body = new
            {
                business_name = request.BusinessName,
                settlement_bank = request.BankCode,
                account_number = request.AccountNumber,
                // the share that stays with the platform (the main account), the rest goes to the creator
                percentage_charge = request.PlatformPercentage.ToString("0.##", CultureInfo.InvariantCulture),
            };

            try
            {
                return await Send<T>(method, url, JsonContent.Create(body), cancellationToken);
            }
            catch (ProviderRefusedException e) when (e.StatusCode is HttpStatusCode.UnprocessableEntity
                                                         or HttpStatusCode.BadRequest)
            {
                // the bank or the number was refused
                throw new InvalidAccountException(e.Message);
            }
        }

        private async Task<Envelope<T>> Send<T>(HttpMethod method, string url, HttpContent? content,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_options.SecretKey))
                throw new PaymentProviderException("The Paystack secret key is not set.");

            using var request = new HttpRequestMessage(method, new Uri(BaseUri, url)) {Content = content};
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SecretKey);

            HttpResponseMessage response;
            try
            {
                response = await _client.SendAsync(request, cancellationToken);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                throw new PaymentProviderException("Paystack could not be reached.", e);
            }

            using (response)
            {
                var text = await response.Content.ReadAsStringAsync(cancellationToken);
                Envelope<T>? envelope = null;
                try
                {
                    envelope = JsonSerializer.Deserialize<Envelope<T>>(text, Json);
                }
                catch (JsonException)
                {
                    // not JSON: handled below
                }

                if (!response.IsSuccessStatusCode || envelope?.Status != true)
                {
                    // the key is never part of the message
                    var message = envelope?.Message ?? $"Paystack answered with {(int) response.StatusCode}.";
                    if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                        or HttpStatusCode.TooManyRequests || (int) response.StatusCode >= 500)
                        throw new PaymentProviderException(message);
                    throw new ProviderRefusedException(response.StatusCode, message);
                }

                return envelope;
            }
        }

        private Uri BaseUri => new(_options.BaseUrl.TrimEnd('/') + "/");

        private static readonly JsonSerializerOptions Json = new() {PropertyNameCaseInsensitive = true};

        /// <summary>The provider understood the request and said no (a wrong account number, for example)</summary>
        private class ProviderRefusedException : Exception
        {
            public ProviderRefusedException(HttpStatusCode statusCode, string message) : base(message)
            {
                StatusCode = statusCode;
            }

            public HttpStatusCode StatusCode { get; }
        }

        private class Envelope<T>
        {
            [JsonPropertyName("status")] public bool Status { get; set; }

            [JsonPropertyName("message")] public string? Message { get; set; }

            [JsonPropertyName("data")] public T? Data { get; set; }
        }

        private class BankDto
        {
            [JsonPropertyName("name")] public string? Name { get; set; }

            [JsonPropertyName("code")] public string? Code { get; set; }
        }

        private class ResolveDto
        {
            [JsonPropertyName("account_number")] public string? AccountNumber { get; set; }

            [JsonPropertyName("account_name")] public string? AccountName { get; set; }
        }

        private class InitializeDto
        {
            [JsonPropertyName("authorization_url")] public string? AuthorizationUrl { get; set; }

            [JsonPropertyName("access_code")] public string? AccessCode { get; set; }
        }

        private class TransactionDto
        {
            [JsonPropertyName("id")] public long Id { get; set; }

            [JsonPropertyName("status")] public string? Status { get; set; }

            [JsonPropertyName("reference")] public string? Reference { get; set; }

            [JsonPropertyName("amount")] public long Amount { get; set; }

            [JsonPropertyName("currency")] public string? Currency { get; set; }

            [JsonPropertyName("channel")] public string? Channel { get; set; }

            [JsonPropertyName("paid_at")] public DateTimeOffset? PaidAt { get; set; }
        }

        private class SubaccountDto
        {
            [JsonPropertyName("subaccount_code")] public string? SubaccountCode { get; set; }
        }
    }

    /// <summary>The list of banks hardly ever changes: it is kept for a while, one copy for the whole application</summary>
    public class BankListCache
    {
        private readonly Dictionary<string, (DateTimeOffset At, IReadOnlyList<ProviderBank> Banks)> _entries = new();
        private readonly object _lock = new();

        public bool TryGet(string country, DateTimeOffset now, TimeSpan maxAge, out IReadOnlyList<ProviderBank> banks)
        {
            lock (_lock)
            {
                if (_entries.TryGetValue(country, out var entry) && now - entry.At < maxAge)
                {
                    banks = entry.Banks;
                    return true;
                }
            }

            banks = Array.Empty<ProviderBank>();
            return false;
        }

        public void Set(string country, DateTimeOffset now, IReadOnlyList<ProviderBank> banks)
        {
            lock (_lock)
            {
                _entries[country] = (now, banks);
            }
        }
    }

    /// <summary>Used while payments are not set up: every call says so</summary>
    public class UnavailablePaymentProvider : IPaymentProvider
    {
        private static PaymentProviderException NotSetUp => new("Payments are not set up.");

        public Task<IReadOnlyList<ProviderBank>> ListBanks(string country, CancellationToken cancellationToken = default)
            => throw NotSetUp;

        public Task<ResolvedAccount> ResolveAccount(string bankCode, string accountNumber,
            CancellationToken cancellationToken = default) => throw NotSetUp;

        public Task<string> CreateSubaccount(SubaccountRequest request, CancellationToken cancellationToken = default)
            => throw NotSetUp;

        public Task UpdateSubaccount(string subaccountCode, SubaccountRequest request,
            CancellationToken cancellationToken = default) => throw NotSetUp;

        public Task<CheckoutSession> InitializeCheckout(CheckoutRequest request,
            CancellationToken cancellationToken = default) => throw NotSetUp;

        public Task<VerifiedTransaction> VerifyTransaction(string reference,
            CancellationToken cancellationToken = default) => throw NotSetUp;

        public bool IsValidWebhookSignature(byte[] rawBody, string? signature) => false;
    }
}
