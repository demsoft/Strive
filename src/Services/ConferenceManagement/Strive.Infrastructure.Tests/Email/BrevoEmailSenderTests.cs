using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Strive.Core.Services.Reminders;
using Strive.Infrastructure.Email;
using Xunit;

namespace Strive.Infrastructure.Tests.Email
{
    public class BrevoEmailSenderTests
    {
        private const string Key = "xkeysib-secretvalue";

        private class Handler : HttpMessageHandler
        {
            public HttpRequestMessage? Request { get; private set; }
            public string Body { get; private set; } = "";
            public HttpStatusCode Code { get; set; } = HttpStatusCode.Created;
            public string Answer { get; set; } = "{\"messageId\":\"1\"}";
            public bool Throw { get; set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                if (Throw) throw new HttpRequestException("no network");
                Request = request;
                Body = await request.Content!.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(Code) {Content = new StringContent(Answer, Encoding.UTF8, "application/json")};
            }
        }

        private static BrevoEmailSender Sender(Handler handler, string? key = Key,
            string from = "Gbedu <no-reply@goserp.co.uk>") =>
            new(new HttpClient(handler), Options.Create(new EmailOptions {BrevoApiKey = key, From = from}));

        [Fact]
        public async Task Send_PostsTheMailToBrevoWithTheKeyInTheHeader()
        {
            var handler = new Handler();

            await Sender(handler).Send("ada@example.com", "Hello", "<b>Hi</b>", "Hi");

            Assert.Equal(BrevoEmailSender.Endpoint, handler.Request!.RequestUri!.ToString());
            Assert.Equal(Key, handler.Request.Headers.GetValues("api-key").Single());
            using var json = JsonDocument.Parse(handler.Body);
            var root = json.RootElement;
            Assert.Equal("Gbedu", root.GetProperty("sender").GetProperty("name").GetString());
            Assert.Equal("no-reply@goserp.co.uk", root.GetProperty("sender").GetProperty("email").GetString());
            Assert.Equal("ada@example.com", root.GetProperty("to")[0].GetProperty("email").GetString());
            Assert.Equal("Hello", root.GetProperty("subject").GetString());
            Assert.Equal("<b>Hi</b>", root.GetProperty("htmlContent").GetString());
            Assert.Equal("Hi", root.GetProperty("textContent").GetString());
            Assert.DoesNotContain(Key, handler.Body);
        }

        [Fact]
        public async Task Send_BrevoRefuses_ThrowsWithTheReasonButNotTheKey()
        {
            var handler = new Handler {Code = HttpStatusCode.Unauthorized, Answer = "{\"message\":\"Key not found\"}"};

            var error = await Assert.ThrowsAsync<EmailException>(() => Sender(handler).Send("a@b.co", "s", "h", "t"));

            Assert.Contains("401", error.Message);
            Assert.Contains("Key not found", error.Message);
            Assert.DoesNotContain(Key, error.Message);
        }

        [Fact]
        public async Task Send_NetworkDown_ThrowsEmailException()
        {
            await Assert.ThrowsAsync<EmailException>(() =>
                Sender(new Handler {Throw = true}).Send("a@b.co", "s", "h", "t"));
        }

        [Fact]
        public async Task Send_WithoutKey_IsNotConfiguredAndThrows()
        {
            var sender = Sender(new Handler(), key: null);

            Assert.False(sender.IsConfigured);
            await Assert.ThrowsAsync<EmailException>(() => sender.Send("a@b.co", "s", "h", "t"));
        }

        [Theory]
        [InlineData("Gbedu <no-reply@goserp.co.uk>", "Gbedu", "no-reply@goserp.co.uk")]
        [InlineData("\"Gbedu Live\" <no-reply@goserp.co.uk>", "Gbedu Live", "no-reply@goserp.co.uk")]
        [InlineData("no-reply@goserp.co.uk", null, "no-reply@goserp.co.uk")]
        [InlineData("<no-reply@goserp.co.uk>", null, "no-reply@goserp.co.uk")]
        public void ParseSender_SplitsNameAndAddress(string from, string? name, string address)
        {
            Assert.Equal((name, address), BrevoEmailSender.ParseSender(from));
        }
    }
}
