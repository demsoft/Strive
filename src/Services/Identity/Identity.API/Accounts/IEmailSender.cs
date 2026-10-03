#nullable enable
using System.Threading.Tasks;

namespace Identity.API.Accounts
{
    public record EmailMessage(string To, string Subject, string TextBody, string HtmlBody);

    public interface IEmailSender
    {
        Task SendAsync(EmailMessage message);
    }
}
