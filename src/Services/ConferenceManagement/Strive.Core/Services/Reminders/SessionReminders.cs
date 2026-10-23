using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Strive.Core.Interfaces;
using Strive.Core.Services.Payments.Gateways;
using Strive.Core.Services.Payments.Sessions;

namespace Strive.Core.Services.Reminders
{
    /// <summary>Sends the reminder of every session that starts soon and has not been reminded yet</summary>
    /// <returns>The number of mails that were sent</returns>
    public record SendSessionRemindersRequest : IRequest<SuccessOrError<int>>;

    public static class ReminderMail
    {
        /// <summary>
        ///     The time as the people of the creator read it: Nigeria has one time zone all year (UTC+1), everything else
        ///     is shown in UTC so that nobody is told a wrong hour.
        /// </summary>
        public static string FormatTime(DateTimeOffset startsAt, string country)
        {
            var nigeria = string.Equals(country, "NG", StringComparison.OrdinalIgnoreCase);
            var local = nigeria ? startsAt.ToOffset(TimeSpan.FromHours(1)) : startsAt.ToUniversalTime();
            return $"{local:ddd d MMM, HH:mm} {(nigeria ? "WAT" : "UTC")}";
        }

        public static (string Subject, string Html, string Text) Build(string title, string creatorName, string when,
            string link)
        {
            var subject = $"Starting soon: {title}";
            var text = $"{creatorName} starts \"{title}\" at {when}.\n\nJoin here: {link}\n\nYou get this mail because you have a ticket.";
            var html =
                $"<p><b>{WebUtility.HtmlEncode(creatorName)}</b> starts <b>{WebUtility.HtmlEncode(title)}</b> at {WebUtility.HtmlEncode(when)}.</p>" +
                $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">Join the session</a></p>" +
                "<p style=\"color:#888\">You get this mail because you have a ticket.</p>";
            return (subject, html, text);
        }
    }

    public class SendSessionRemindersUseCase : IRequestHandler<SendSessionRemindersRequest, SuccessOrError<int>>
    {
        private readonly ISessionRepo _sessions;
        private readonly ICreatorRepo _creators;
        private readonly IOrderRepo _orders;
        private readonly IEmailSender _email;
        private readonly RemindersOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<SendSessionRemindersUseCase> _logger;

        public SendSessionRemindersUseCase(ISessionRepo sessions, ICreatorRepo creators, IOrderRepo orders,
            IEmailSender email, IOptions<RemindersOptions> options, TimeProvider timeProvider,
            ILogger<SendSessionRemindersUseCase> logger)
        {
            _sessions = sessions;
            _creators = creators;
            _orders = orders;
            _email = email;
            _options = options.Value;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<SuccessOrError<int>> Handle(SendSessionRemindersRequest request,
            CancellationToken cancellationToken)
        {
            // nothing is used up while mails cannot be sent: the reminder goes out once they can
            if (!_options.Enabled || !_email.IsConfigured) return 0;

            var now = _timeProvider.GetUtcNow();
            var sent = 0;
            foreach (var session in await _sessions.FindDueForReminder(now, now.AddMinutes(_options.LeadMinutes)))
            {
                // only one instance sends it, and only once
                if (!await _sessions.TryClaimReminder(session.SessionId, now)) continue;

                var creator = await _creators.FindById(session.CreatorId);
                var recipients = (await _orders.FindPaidOfSession(session.SessionId))
                    .Where(x => x.Status == OrderStatus.Paid && !string.IsNullOrWhiteSpace(x.Email))
                    .Select(x => x.Email.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (recipients.Count == 0) continue;

                var link = $"{_options.LinkBaseUrl.TrimEnd('/')}/s/{Uri.EscapeDataString(session.SessionId)}";
                var mail = ReminderMail.Build(session.Title, creator?.DisplayName ?? "Your host",
                    ReminderMail.FormatTime(session.StartsAt!.Value, creator?.Country ?? "NG"), link);

                var delivered = 0;
                foreach (var recipient in recipients)
                    try
                    {
                        await _email.Send(recipient, mail.Subject, mail.Html, mail.Text, cancellationToken);
                        delivered++;
                    }
                    catch (EmailException e)
                    {
                        _logger.LogWarning(e, "The reminder of session {sessionId} could not be sent to one person",
                            session.SessionId);
                    }

                // nobody got it (the mail service is down): try again at the next round
                if (delivered == 0) await _sessions.ReleaseReminder(session.SessionId);
                sent += delivered;
            }

            return sent;
        }
    }
}
