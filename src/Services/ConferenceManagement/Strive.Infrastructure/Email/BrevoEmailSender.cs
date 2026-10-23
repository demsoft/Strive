using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Strive.Core.Services.Reminders;

namespace Strive.Infrastructure.Email
{
    /// <summary>Sends mails through the transactional API of Brevo (https://developers.brevo.com).</summary>
    public class BrevoEmailSender : IEmailSender
    {
        public const string Endpoint = "https://api.brevo.com/v3/smtp/email";

        private readonly HttpClient _client;
        private readonly EmailOptions _options;

        public BrevoEmailSender(HttpClient client, IOptions<EmailOptions> options)
        {
            _client = client;
            _options = options.Value;
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.BrevoApiKey);

        public async Task Send(string toEmail, string subject, string htmlBody, string textBody,
            CancellationToken cancellationToken = default)
        {
            if (!IsConfigured) throw new EmailException("Email is not set up.");

            var (name, address) = ParseSender(_options.From);
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = JsonContent.Create(new
                {
                    sender = new {name, email = address},
                    to = new[] {new {email = toEmail}},
                    subject,
                    htmlContent = htmlBody,
                    textContent = textBody,
                }),
            };
            request.Headers.Add("api-key", _options.BrevoApiKey);
            request.Headers.Add("accept", "application/json");

            HttpResponseMessage response;
            try
            {
                response = await _client.SendAsync(request, cancellationToken);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                throw new EmailException("Brevo could not be reached.", e);
            }

            using (response)
            {
                // the body tells what is wrong (unknown sender, wrong key); it does not contain the key
                if (!response.IsSuccessStatusCode)
                    throw new EmailException(
                        $"Brevo answered {(int) response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
            }
        }

        /// <summary>"Gbedu &lt;no-reply@example.com&gt;" gives the name and the address; a bare address has no name</summary>
        public static (string? Name, string Address) ParseSender(string from)
        {
            var open = from.LastIndexOf('<');
            var close = from.LastIndexOf('>');
            if (open >= 0 && close > open)
            {
                var name = from[..open].Trim().Trim('"');
                return (name.Length == 0 ? null : name, from[(open + 1)..close].Trim());
            }

            return (null, from.Trim());
        }
    }

    /// <summary>Used while no mail key is set: nothing can be sent</summary>
    public class UnavailableEmailSender : IEmailSender
    {
        public bool IsConfigured => false;

        public Task Send(string toEmail, string subject, string htmlBody, string textBody,
            CancellationToken cancellationToken = default) => throw new EmailException("Email is not set up.");
    }
}
