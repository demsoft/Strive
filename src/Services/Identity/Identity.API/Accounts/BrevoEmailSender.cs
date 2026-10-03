#nullable enable
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Identity.API.Accounts
{
    /// <summary>Sends the mails through the transactional API of Brevo (https://developers.brevo.com).</summary>
    public class BrevoEmailSender : IEmailSender
    {
        public const string Endpoint = "https://api.brevo.com/v3/smtp/email";

        private readonly HttpClient _client;
        private readonly string _apiKey;
        private readonly MailboxAddress _from;

        public BrevoEmailSender(HttpClient client, IOptions<AccountsOptions> options)
        {
            var email = options.Value.Email;
            _client = client;
            _apiKey = email.BrevoApiKey ?? throw new InvalidOperationException("Accounts:Email:BrevoApiKey is missing.");
            _from = MailboxAddress.Parse(email.From);
        }

        public async Task SendAsync(EmailMessage message)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = JsonContent.Create(new
                {
                    sender = new {name = _from.Name, email = _from.Address},
                    to = new[] {new {email = message.To}},
                    subject = message.Subject,
                    htmlContent = message.HtmlBody,
                    textContent = message.TextBody,
                }),
            };
            request.Headers.Add("api-key", _apiKey);
            request.Headers.Add("accept", "application/json");

            using var response = await _client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                // the body tells what is wrong (unknown sender, wrong key); it does not contain the key
                throw new InvalidOperationException(
                    $"Brevo answered {(int) response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
    }
}
