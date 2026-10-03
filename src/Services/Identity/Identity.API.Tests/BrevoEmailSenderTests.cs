using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Identity.API.Accounts;
using Microsoft.Extensions.Options;
using Xunit;

namespace Identity.API.Tests
{
    public class BrevoEmailSenderTests
    {
        private class CapturingHandler : HttpMessageHandler
        {
            public HttpRequestMessage? Request { get; private set; }
            public string? Body { get; private set; }
            public HttpStatusCode Status { get; set; } = HttpStatusCode.Created;
            public string Response { get; set; } = "{\"messageId\":\"<1@x>\"}";

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                Request = request;
                Body = request.Content == null ? null : await request.Content.ReadAsStringAsync(token);
                return new HttpResponseMessage(Status) {Content = new StringContent(Response)};
            }
        }

        private static (BrevoEmailSender sender, CapturingHandler handler) Create(string from = "GODP Consulting <training@godp.co.uk>")
        {
            var handler = new CapturingHandler();
            var options = Options.Create(new AccountsOptions {Email = {BrevoApiKey = "xkeysib-test", From = from}});
            return (new BrevoEmailSender(new HttpClient(handler), options), handler);
        }

        [Fact]
        public async Task Send_PostsTheMailToTheBrevoApiWithTheKey()
        {
            var (sender, handler) = Create();

            await sender.SendAsync(new EmailMessage("ann@example.com", "Hello", "plain text", "<b>html</b>"));

            Assert.Equal("https://api.brevo.com/v3/smtp/email", handler.Request!.RequestUri!.ToString());
            Assert.Equal(HttpMethod.Post, handler.Request.Method);
            Assert.Equal("xkeysib-test", Assert.Single(handler.Request.Headers.GetValues("api-key")));

            using var json = JsonDocument.Parse(handler.Body!);
            var root = json.RootElement;
            Assert.Equal("GODP Consulting", root.GetProperty("sender").GetProperty("name").GetString());
            Assert.Equal("training@godp.co.uk", root.GetProperty("sender").GetProperty("email").GetString());
            Assert.Equal("ann@example.com", root.GetProperty("to")[0].GetProperty("email").GetString());
            Assert.Equal("Hello", root.GetProperty("subject").GetString());
            Assert.Equal("<b>html</b>", root.GetProperty("htmlContent").GetString());
            Assert.Equal("plain text", root.GetProperty("textContent").GetString());
        }

        [Fact]
        public async Task Send_ABrevoErrorBecomesAnException_WithoutTheKey()
        {
            var (sender, handler) = Create();
            handler.Status = HttpStatusCode.BadRequest;
            handler.Response = "{\"code\":\"invalid_parameter\",\"message\":\"sender is not valid\"}";

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sender.SendAsync(new EmailMessage("ann@example.com", "Hello", "t", "h")));

            Assert.Contains("400", error.Message);
            Assert.Contains("sender is not valid", error.Message);
            Assert.DoesNotContain("xkeysib", error.Message);
        }

        [Fact]
        public void TheKeyIsRequired()
        {
            var options = Options.Create(new AccountsOptions());

            Assert.Throws<InvalidOperationException>(() => new BrevoEmailSender(new HttpClient(), options));
        }
    }
}
