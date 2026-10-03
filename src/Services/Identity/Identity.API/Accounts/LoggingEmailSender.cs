#nullable enable
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Identity.API.Accounts
{
    /// <summary>Used when no SMTP server is configured: the mail is not sent, only noted in the log.</summary>
    public class LoggingEmailSender : IEmailSender
    {
        private readonly ILogger<LoggingEmailSender> _logger;

        public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
        {
            _logger = logger;
        }

        public Task SendAsync(EmailMessage message)
        {
            _logger.LogWarning("No SMTP server is configured (Accounts:Email:Host), the email \"{Subject}\" was not sent",
                message.Subject);
            return Task.CompletedTask;
        }
    }
}
