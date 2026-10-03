#nullable enable
using System;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Identity.API.Accounts
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly EmailOptions _options;

        public SmtpEmailSender(IOptions<AccountsOptions> options)
        {
            _options = options.Value.Email;
            if (string.IsNullOrWhiteSpace(_options.Host))
                throw new InvalidOperationException(
                    "Accounts:Email:Host is not configured. Accounts need an SMTP server to send the confirmation and " +
                    "password reset emails (in development ./compose.sh starts a local inbox).");
        }

        public async Task SendAsync(EmailMessage message)
        {
            var mime = new MimeMessage();
            mime.From.Add(MailboxAddress.Parse(_options.From));
            mime.To.Add(MailboxAddress.Parse(message.To));
            mime.Subject = message.Subject;
            mime.Body = new BodyBuilder {TextBody = message.TextBody, HtmlBody = message.HtmlBody}.ToMessageBody();

            using var client = new SmtpClient();
            var security = !_options.UseTls ? SecureSocketOptions.None
                : _options.Port == 465 ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;
            await client.ConnectAsync(_options.Host, _options.Port, security);
            if (!string.IsNullOrEmpty(_options.User)) await client.AuthenticateAsync(_options.User, _options.Password);
            await client.SendAsync(mime);
            await client.DisconnectAsync(true);
        }
    }
}
