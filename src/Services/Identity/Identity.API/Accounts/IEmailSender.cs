#nullable enable
using System.Threading.Tasks;

namespace Identity.API.Accounts
{
    /// <param name="SenderName">The name shown as the sender instead of the configured one (the address stays)</param>
    public record EmailMessage(string To, string Subject, string TextBody, string HtmlBody, string? SenderName = null);

    public interface IEmailSender
    {
        Task SendAsync(EmailMessage message);
    }
}
