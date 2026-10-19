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

        private class ThrowingHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken) => throw new HttpRequestException("no network");
        }
    }
}
