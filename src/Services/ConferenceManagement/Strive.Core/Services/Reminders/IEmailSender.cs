using System;
using System.Threading;
using System.Threading.Tasks;

namespace Strive.Core.Services.Reminders
{
    public interface IEmailSender
    {
        /// <summary>Whether mails can be sent at all (a key is set)</summary>
        bool IsConfigured { get; }

        /// <summary>Sends one mail. Throws <see cref="EmailException" /> if it was not accepted.</summary>
        Task Send(string toEmail, string subject, string htmlBody, string textBody,
            CancellationToken cancellationToken = default);
    }

    public class EmailException : Exception
    {
        public EmailException(string message, Exception? inner = null) : base(message, inner)
        {
        }
    }
}
