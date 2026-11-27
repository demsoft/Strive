using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Strive.Core.Services.Reminders;

namespace Strive.IntegrationTests._Helpers
{
    /// <summary>Stands in for the mail service: it remembers what it was asked to send.</summary>
    public class FakeEmailSender : IEmailSender
    {
        public record Mail(string To, string Subject, string Html, string Text, string? SenderName = null);

        public ConcurrentQueue<Mail> Sent { get; } = new();

        public bool Configured { get; set; } = true;

        /// <summary>The mail service refuses every mail while this is set</summary>
        public bool Failing { get; set; }

        public bool IsConfigured => Configured;

        public Task Send(string toEmail, string subject, string htmlBody, string textBody,
            CancellationToken cancellationToken = default, string? senderName = null)
        {
            if (Failing) throw new EmailException("Brevo answered 401.");
            Sent.Enqueue(new Mail(toEmail, subject, htmlBody, textBody, senderName));
            return Task.CompletedTask;
        }
    }
}
