using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Strive.Core.Services.Payments;
using Strive.Core.Services.Payments.Gateways;
using Strive.Infrastructure.Payments;
using Xunit;

namespace Strive.Infrastructure.Tests.Payments
{
    public class PaystackPaymentProviderTests
    {
        private const string Key = "sk_test_secretvalue";

        private class Handler : HttpMessageHandler
        {
            public List<(HttpRequestMessage Request, string Body)> Calls { get; } = new();

            public Func<HttpRequestMessage, (HttpStatusCode Code, string Json)> Answer { get; set; } =
                _ => (HttpStatusCode.OK, "{\"status\":true,\"data\":null}");

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
                Calls.Add((request, body));
                var (code, json) = Answer(request);
                return new HttpResponseMessage(code) {Content = new StringContent(json, Encoding.UTF8, "application/json")};
            }
        }

        private class Time : TimeProvider
        {
            public DateTimeOffset Now { get; set; } = DateTimeOffset.FromUnixTimeSeconds(1700000000);

            public override DateTimeOffset GetUtcNow() => Now;
        }

        private static (PaystackPaymentProvider provider, Handler handler) Create(string? key = Key, Time? time = null)
        {
            var handler = new Handler();
            var options = Options.Create(new PaymentsOptions {Paystack = {SecretKey = key}});
            var provider = new PaystackPaymentProvider(new HttpClient(handler), options, time ?? new Time(),
                new BankListCache());
            return (provider, handler);
        }

        private static string Banks(int count, int offset = 0) => "{\"status\":true,\"message\":\"Banks retrieved\",\"data\":[" +
            string.Join(",", Enumerable.Range(offset, count).Select(i => $"{{\"name\":\"Bank {i:000}\",\"code\":\"{i:000}\"}}")) + "]}";

        [Fact]
        public async Task ResolveAccount_SendsTheKeyAndReadsTheName()
        {
            var (provider, handler) = Create();
            handler.Answer = _ => (HttpStatusCode.OK,
                "{\"status\":true,\"data\":{\"account_number\":\"0123456789\",\"account_name\":\"ADA LOVELACE\"}}");

            var account = await provider.ResolveAccount("058", "0123456789");

            Assert.Equal("ADA LOVELACE", account.AccountName);
            var request = handler.Calls.Single().Request;
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal(Key, request.Headers.Authorization.Parameter);
            Assert.Equal("https://api.paystack.co/bank/resolve?account_number=0123456789&bank_code=058",
                request.RequestUri!.ToString());
        }

        [Theory]
        [InlineData(HttpStatusCode.UnprocessableEntity)]
        [InlineData(HttpStatusCode.BadRequest)]
        public async Task ResolveAccount_NoSuchAccount_IsAnInvalidAccount(HttpStatusCode code)
        {
            var (provider, handler) = Create();
            handler.Answer = _ => (code, "{\"status\":false,\"message\":\"Could not resolve account name.\"}");

            await Assert.ThrowsAsync<InvalidAccountException>(() => provider.ResolveAccount("058", "0000000000"));
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.TooManyRequests)]
        public async Task ResolveAccount_TheServiceHasAProblem_IsNotMistakenForABadAccount(HttpStatusCode code)
        {
            var (provider, handler) = Create();
            handler.Answer = _ => (code, "{\"status\":false,\"message\":\"Invalid key\"}");

            await Assert.ThrowsAsync<PaymentProviderException>(() => provider.ResolveAccount("058", "0123456789"));
        }

        [Fact]
        public async Task NoSecretKey_NothingIsSent()
        {
            var (provider, handler) = Create(key: "");

            await Assert.ThrowsAsync<PaymentProviderException>(() => provider.ResolveAccount("058", "0123456789"));

            Assert.Empty(handler.Calls);
        }

        [Fact]
        public async Task TheSecretKeyIsNeverPartOfAnErrorMessage()
        {
            var (provider, handler) = Create();
            handler.Answer = _ => (HttpStatusCode.Unauthorized, "not json");

            var error = await Assert.ThrowsAsync<PaymentProviderException>(() => provider.ListBanks("NG"));

            Assert.DoesNotContain(Key, error.Message);
        }

        [Fact]
        public async Task CreateSubaccount_SendsTheBankAndThePlatformShare()
        {
            var (provider, handler) = Create();
            handler.Answer = _ => (HttpStatusCode.Created,
                "{\"status\":true,\"data\":{\"subaccount_code\":\"ACCT_abc123\"}}");

            var code = await provider.CreateSubaccount(new SubaccountRequest("Ada Lovelace", "058", "0123456789", 10));

            Assert.Equal("ACCT_abc123", code);
            var (request, body) = handler.Calls.Single();
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://api.paystack.co/subaccount", request.RequestUri!.ToString());
            Assert.Contains("\"business_name\":\"Ada Lovelace\"", body);
            Assert.Contains("\"settlement_bank\":\"058\"", body);
            Assert.Contains("\"account_number\":\"0123456789\"", body);
            Assert.Contains("\"percentage_charge\":\"10\"", body);
        }

        [Fact]
        public async Task CreateSubaccount_TheBankRefuses_IsAnInvalidAccount()
        {
            var (provider, handler) = Create();
            handler.Answer = _ => (HttpStatusCode.UnprocessableEntity, "{\"status\":false,\"message\":\"Invalid account\"}");

            await Assert.ThrowsAsync<InvalidAccountException>(() =>
                provider.CreateSubaccount(new SubaccountRequest("Ada", "058", "0123456789", 10)));
        }

        [Fact]
        public async Task CreateSubaccount_AnswerWithoutACode_Fails()
        {
            var (provider, handler) = Create();
            handler.Answer = _ => (HttpStatusCode.OK, "{\"status\":true,\"data\":{}}");

            await Assert.ThrowsAsync<PaymentProviderException>(() =>
                provider.CreateSubaccount(new SubaccountRequest("Ada", "058", "0123456789", 10)));
        }

        [Fact]
        public async Task UpdateSubaccount_UsesPutOnTheCode()
        {
            var (provider, handler) = Create();

            await provider.UpdateSubaccount("ACCT_abc123", new SubaccountRequest("Ada", "044", "9876543210", 12.5m));

            var (request, body) = handler.Calls.Single();
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.EndsWith("/subaccount/ACCT_abc123", request.RequestUri!.ToString());
            Assert.Contains("\"percentage_charge\":\"12.5\"", body);
        }

        [Fact]
        public async Task ListBanks_FollowsThePagesUntilTheLastOne()
        {
            var (provider, handler) = Create();
            handler.Answer = r => r.RequestUri!.Query.Contains("page=1")
                ? (HttpStatusCode.OK, Banks(100))
                : (HttpStatusCode.OK, Banks(30, 100));

            var banks = await provider.ListBanks("NG");

            Assert.Equal(130, banks.Count);
            Assert.Equal(2, handler.Calls.Count);
            Assert.Contains("country=nigeria", handler.Calls[0].Request.RequestUri!.Query);
        }

        [Fact]
        public async Task ListBanks_IsKeptForAnHour()
        {
            var time = new Time();
            var (provider, handler) = Create(time: time);
            handler.Answer = _ => (HttpStatusCode.OK, Banks(5, 500));
            await provider.ListBanks("GH");

            await provider.ListBanks("GH");
            Assert.Single(handler.Calls);

            time.Now = time.Now.AddMinutes(61);
            await provider.ListBanks("GH");
            Assert.Equal(2, handler.Calls.Count);
        }

        [Fact]
        public async Task ListBanks_SendsTheCountryByName()
        {
            var (provider, handler) = Create();
            handler.Answer = _ => (HttpStatusCode.OK, Banks(2, 700));

            await provider.ListBanks("ZA");

            Assert.Contains("country=south%20africa", handler.Calls.Single().Request.RequestUri!.Query);
        }

        [Fact]
        public async Task TheServiceCannotBeReached_IsAProviderProblem()
        {
            var handler = new ThrowingHandler();
            var provider = new PaystackPaymentProvider(new HttpClient(handler),
                Options.Create(new PaymentsOptions {Paystack = {SecretKey = Key}}), new Time(), new BankListCache());

            await Assert.ThrowsAsync<PaymentProviderException>(() => provider.ResolveAccount("058", "0123456789"));
        }

        // ---- checkout, verification and notifications

        [Fact]
        public async Task InitializeCheckout_SendsTheAmountInKoboTheSubaccountAndTheCommission()
        {
            var (provider, handler) = Create();
            handler.Answer = _ => (HttpStatusCode.OK,
                "{\"status\":true,\"data\":{\"authorization_url\":\"https://checkout.paystack.com/abc\",\"access_code\":\"abc\",\"reference\":\"gbd_1\"}}");

            var session = await provider.InitializeCheckout(new CheckoutRequest("gbd_1", "fan@example.com", 500_000, "NGN",
                "https://gbedu.goserp.co.uk/s/abcd2345", "ACCT_x", 50_000,
                new Dictionary<string, string> {{"sessionId", "abcd2345"}}));

            Assert.Equal("https://checkout.paystack.com/abc", session.AuthorizationUrl);
            var (request, body) = handler.Calls.Single();
            Assert.Equal("https://api.paystack.co/transaction/initialize", request.RequestUri!.ToString());
            Assert.Contains("\"amount\":500000", body);
            Assert.Contains("\"currency\":\"NGN\"", body);
            Assert.Contains("\"reference\":\"gbd_1\"", body);
            Assert.Contains("\"subaccount\":\"ACCT_x\"", body);
            Assert.Contains("\"transaction_charge\":50000", body);
            Assert.Contains("\"bearer\":\"account\"", body);
            Assert.Contains("\"callback_url\":\"https://gbedu.goserp.co.uk/s/abcd2345\"", body);
        }

        [Fact]
        public async Task InitializeCheckout_AnswerWithoutAnAddress_Fails()
        {
            var (provider, handler) = Create();
            handler.Answer = _ => (HttpStatusCode.OK, "{\"status\":true,\"data\":{}}");

            await Assert.ThrowsAsync<PaymentProviderException>(() => provider.InitializeCheckout(
                new CheckoutRequest("r", "a@b.co", 1, "NGN", "https://x", "ACCT", 0, new Dictionary<string, string>())));
        }

        [Theory]
        [InlineData("success", true, false)]
        [InlineData("failed", false, true)]
        [InlineData("reversed", false, true)]
        [InlineData("abandoned", false, false)]
        [InlineData("ongoing", false, false)]
        [InlineData("pending", false, false)]
        public async Task VerifyTransaction_ReadsTheState(string status, bool paid, bool failed)
        {
            var (provider, handler) = Create();
            handler.Answer = _ => (HttpStatusCode.OK,
                "{\"status\":true,\"data\":{\"id\":4099260516,\"status\":\"" + status +
                "\",\"reference\":\"gbd_1\",\"amount\":500000,\"currency\":\"NGN\",\"channel\":\"bank_transfer\",\"paid_at\":\"2026-10-19T10:15:00.000Z\"}}");

            var result = await provider.VerifyTransaction("gbd_1");

            Assert.Equal(paid, result.Paid);
            Assert.Equal(failed, result.Failed);
            Assert.Equal(500_000, result.AmountMinor);
            Assert.Equal("NGN", result.Currency);
            Assert.Equal("bank_transfer", result.Channel);
            Assert.Equal("4099260516", result.ProviderTransactionId);
            Assert.Equal("https://api.paystack.co/transaction/verify/gbd_1", handler.Calls.Single().Request.RequestUri!.ToString());
        }

        [Fact]
        public async Task VerifyTransaction_ReusableCard_GivesItsToken()
        {
            var (provider, handler) = Create();
            handler.Answer = _ => (HttpStatusCode.OK,
                "{\"status\":true,\"data\":{\"id\":42,\"status\":\"success\",\"reference\":\"gbd_1\",\"amount\":500000,\"currency\":\"NGN\",\"channel\":\"card\"," +
                "\"authorization\":{\"authorization_code\":\"AUTH_abc\",\"reusable\":true,\"last4\":\"4081\"}}}");

            var result = await provider.VerifyTransaction("gbd_1");

            Assert.Equal("AUTH_abc", result.AuthorizationCode);
        }

        [Theory]
        [InlineData("{\"authorization_code\":\"AUTH_abc\",\"reusable\":false}")] // bank transfer, USSD
        [InlineData("{\"authorization_code\":\"\",\"reusable\":true}")]
        [InlineData("null")]
        public async Task VerifyTransaction_CardThatCannotBeChargedAgain_GivesNoToken(string authorization)
        {
            var (provider, handler) = Create();
            handler.Answer = _ => (HttpStatusCode.OK,
                "{\"status\":true,\"data\":{\"id\":42,\"status\":\"success\",\"reference\":\"gbd_1\",\"amount\":500000,\"currency\":\"NGN\"," +
                $"\"authorization\":{authorization}}}}}");

            var result = await provider.VerifyTransaction("gbd_1");

            Assert.Null(result.AuthorizationCode);
        }

        [Fact]
        public async Task ChargeAuthorization_SendsTheTokenWithTheSameSplitAsCheckout()
        {
            var (provider, handler) = Create();
            handler.Answer = _ => (HttpStatusCode.OK, "{\"status\":true,\"data\":{\"status\":\"success\"}}");

            await provider.ChargeAuthorization(new ChargeAuthorizationRequest("gbd_renew1", "fan@example.com",
                "AUTH_abc", 2_000_000, "NGN", "ACCT_1", 200_000,
                new Dictionary<string, string> {{"subscriptionId", "sub1"}}));

            var (request, body) = handler.Calls.Single();
            Assert.Equal("https://api.paystack.co/transaction/charge_authorization", request.RequestUri!.ToString());
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(Key, request.Headers.Authorization!.Parameter);
            using var json = System.Text.Json.JsonDocument.Parse(body);
            var root = json.RootElement;
            Assert.Equal("AUTH_abc", root.GetProperty("authorization_code").GetString());
            Assert.Equal(2_000_000, root.GetProperty("amount").GetInt64());
            Assert.Equal("gbd_renew1", root.GetProperty("reference").GetString());
            Assert.Equal("ACCT_1", root.GetProperty("subaccount").GetString());
            Assert.Equal(200_000, root.GetProperty("transaction_charge").GetInt64());
            Assert.Equal("account", root.GetProperty("bearer").GetString());
            Assert.DoesNotContain(Key, body);
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task ChargeAuthorization_Refused_IsAProviderException(HttpStatusCode code)
        {
            var (provider, handler) = Create();
            handler.Answer = _ => (code, "{\"status\":false,\"message\":\"Invalid authorization code\"}");

            await Assert.ThrowsAsync<PaymentProviderException>(() => provider.ChargeAuthorization(
                new ChargeAuthorizationRequest("r", "a@b.co", "AUTH_x", 1000, "NGN", "ACCT_1", 100,
                    new Dictionary<string, string>())));
        }

        [Fact]
        public async Task VerifyTransaction_UnknownReference_IsNotFound()
        {
            var (provider, handler) = Create();
            handler.Answer = _ => (HttpStatusCode.NotFound, "{\"status\":false,\"message\":\"Transaction reference not found\"}");

            await Assert.ThrowsAsync<TransactionNotFoundException>(() => provider.VerifyTransaction("gbd_nope"));
        }

        [Fact]
        public async Task VerifyTransaction_TheServiceHasAProblem_IsNotMistakenForNotPaid()
        {
            var (provider, handler) = Create();
            handler.Answer = _ => (HttpStatusCode.InternalServerError, "{\"status\":false}");

            await Assert.ThrowsAsync<PaymentProviderException>(() => provider.VerifyTransaction("gbd_1"));
        }

        private static string Sign(string key, byte[] body)
        {
            using var hmac = new System.Security.Cryptography.HMACSHA512(Encoding.UTF8.GetBytes(key));
            return Convert.ToHexString(hmac.ComputeHash(body)).ToLowerInvariant();
        }

        [Fact]
        public void Webhook_SignatureOverTheExactBytesWithTheSecretKey_IsValid()
        {
            var (provider, _) = Create();
            var body = Encoding.UTF8.GetBytes("{\"event\":\"charge.success\",\"data\":{\"reference\":\"gbd_1\"}}");

            Assert.True(provider.IsValidWebhookSignature(body, Sign(Key, body)));
            Assert.True(provider.IsValidWebhookSignature(body, Sign(Key, body).ToUpperInvariant()));
        }

        [Fact]
        public void Webhook_AnythingElse_IsNotValid()
        {
            var (provider, _) = Create();
            var body = Encoding.UTF8.GetBytes("{\"event\":\"charge.success\"}");
            var changed = Encoding.UTF8.GetBytes("{\"event\":\"charge.success\" }");

            Assert.False(provider.IsValidWebhookSignature(body, Sign("another-key", body)));
            // one space more in the body: the signature of the original does not fit
            Assert.False(provider.IsValidWebhookSignature(changed, Sign(Key, body)));
            Assert.False(provider.IsValidWebhookSignature(body, null));
            Assert.False(provider.IsValidWebhookSignature(body, ""));
            Assert.False(provider.IsValidWebhookSignature(body, "not hex"));
        }

        [Fact]
        public void Webhook_WithoutASecretKey_NothingIsValid()
        {
            var (provider, _) = Create(key: "");
            var body = Encoding.UTF8.GetBytes("{}");

            Assert.False(provider.IsValidWebhookSignature(body, Sign("", body)));
        }

        private class ThrowingHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken) => throw new HttpRequestException("no network");
        }
    }
}
