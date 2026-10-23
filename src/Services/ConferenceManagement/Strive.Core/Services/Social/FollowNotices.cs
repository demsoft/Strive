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
using Strive.Core.Services.Payments;
using Strive.Core.Services.Payments.Gateways;
using Strive.Core.Services.Payments.Sessions;
using Strive.Core.Services.Payments.UseCases;
using Strive.Core.Services.Reminders;
using Strive.Core.Services.Videos;

namespace Strive.Core.Services.Social
{
    /// <summary>
    ///     A follower asks to get mail when the creator posts. The address is not trusted until its owner followed the link
    ///     in the mail that is sent to it.
    /// </summary>
    public record StartEmailNoticesRequest(string UserId, string CreatorId, string Email)
        : IRequest<SuccessOrError<Unit>>;

    /// <summary>A follower turns the mail off (from the page of the creator)</summary>
    public record StopEmailNoticesRequest(string UserId, string CreatorId) : IRequest<SuccessOrError<Unit>>;

    /// <summary>The owner of the address followed the link in the mail</summary>
    public record ConfirmEmailNoticesRequest(string Token) : IRequest<SuccessOrError<NoticeAnswer>>;

    /// <summary>The link at the bottom of every mail: no sign in needed, the secret in the link is the proof</summary>
    public record StopEmailNoticesByTokenRequest(string Token) : IRequest<SuccessOrError<NoticeAnswer>>;

    public record NoticeAnswer(string CreatorName, NoticeStatus Status);

    /// <summary>Tells the followers who asked for mail about the videos and sessions that are new</summary>
    /// <returns>The number of mails that were sent</returns>
    public record SendFollowerNoticesRequest : IRequest<SuccessOrError<int>>;

    public static class NoticeMail
    {
        public static string NewToken() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

        private static string Link(RemindersOptions options, string path) =>
            $"{options.LinkBaseUrl.TrimEnd('/')}/{path}";

        public static string ConfirmLink(RemindersOptions options, string token) =>
            Link(options, "n/confirm/" + Uri.EscapeDataString(token));

        public static string StopLink(RemindersOptions options, string token) =>
            Link(options, "n/stop/" + Uri.EscapeDataString(token));

        public static (string Subject, string Html, string Text) Confirmation(string creatorName, string confirmLink)
        {
            var subject = $"Confirm: mail when {creatorName} posts";
            var text = $"You asked to get a mail when {creatorName} posts a new video or schedules a session.\n\n" +
                       $"Confirm that this address is yours: {confirmLink}\n\nIf this was not you, ignore this mail. Nothing will be sent.";
            var html = $"<p>You asked to get a mail when <b>{WebUtility.HtmlEncode(creatorName)}</b> posts a new video or schedules a session.</p>" +
                       $"<p><a href=\"{WebUtility.HtmlEncode(confirmLink)}\">Yes, send me mail</a></p>" +
                       "<p style=\"color:#888\">If this was not you, ignore this mail. Nothing will be sent.</p>";
            return (subject, html, text);
        }

        public static (string Subject, string Html, string Text) News(string creatorName, string headline,
            string link, string linkLabel, string stopLink)
        {
            var subject = $"{creatorName}: {headline}";
            var text = $"{creatorName}: {headline}\n\n{linkLabel}: {link}\n\n" +
                       $"You get this mail because you asked to hear from {creatorName}. Stop it with one click: {stopLink}";
            var html = $"<p><b>{WebUtility.HtmlEncode(creatorName)}</b>: {WebUtility.HtmlEncode(headline)}</p>" +
                       $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">{WebUtility.HtmlEncode(linkLabel)}</a></p>" +
                       $"<p style=\"color:#888\">You get this mail because you asked to hear from {WebUtility.HtmlEncode(creatorName)}. " +
                       $"<a href=\"{WebUtility.HtmlEncode(stopLink)}\">Stop these mails</a></p>";
            return (subject, html, text);
        }
    }

    public class StartEmailNoticesUseCase : IRequestHandler<StartEmailNoticesRequest, SuccessOrError<Unit>>
    {
        private readonly IFollowRepo _follows;
        private readonly ICreatorRepo _creators;
        private readonly IEmailSender _email;
        private readonly RemindersOptions _options;
        private readonly ILogger<StartEmailNoticesUseCase> _logger;

        public StartEmailNoticesUseCase(IFollowRepo follows, ICreatorRepo creators, IEmailSender email,
            IOptions<RemindersOptions> options, ILogger<StartEmailNoticesUseCase> logger)
        {
            _follows = follows;
            _creators = creators;
            _email = email;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<SuccessOrError<Unit>> Handle(StartEmailNoticesRequest request,
            CancellationToken cancellationToken)
        {
            if (!_options.Enabled || !_email.IsConfigured) return SocialError.EmailNotAvailable;
            if (!SessionRules.IsValidEmail(request.Email?.Trim())) return SocialError.InvalidEmail;

            var follow = await _follows.Find(request.UserId, request.CreatorId);
            if (follow == null) return SocialError.NotFollowing;
            var creator = await _creators.FindById(request.CreatorId);
            if (creator == null) return SocialError.CreatorNotFound;

            var address = request.Email!.Trim();
            // a new secret for every request: an old confirm link of another address does not work any more
            var token = NoticeMail.NewToken();
            var mail = NoticeMail.Confirmation(creator.DisplayName, NoticeMail.ConfirmLink(_options, token));
            try
            {
                await _email.Send(address, mail.Subject, mail.Html, mail.Text, cancellationToken);
            }
            catch (EmailException e)
            {
                _logger.LogWarning(e, "The mail to confirm a follower address could not be sent");
                return SocialError.EmailFailed;
            }

            await _follows.SetNotice(follow.FollowId, NoticeStatus.Pending, address, token);
            return Unit.Value;
        }
    }

    public class StopEmailNoticesUseCase : IRequestHandler<StopEmailNoticesRequest, SuccessOrError<Unit>>
    {
        private readonly IFollowRepo _follows;

        public StopEmailNoticesUseCase(IFollowRepo follows)
        {
            _follows = follows;
        }

        public async Task<SuccessOrError<Unit>> Handle(StopEmailNoticesRequest request,
            CancellationToken cancellationToken)
        {
            var follow = await _follows.Find(request.UserId, request.CreatorId);
            if (follow != null && follow.Notice != NoticeStatus.Off)
                await _follows.SetNotice(follow.FollowId, NoticeStatus.Off, null, null);
            return Unit.Value;
        }
    }

    public class ConfirmEmailNoticesUseCase : IRequestHandler<ConfirmEmailNoticesRequest, SuccessOrError<NoticeAnswer>>
    {
        private readonly IFollowRepo _follows;
        private readonly ICreatorRepo _creators;

        public ConfirmEmailNoticesUseCase(IFollowRepo follows, ICreatorRepo creators)
        {
            _follows = follows;
            _creators = creators;
        }

        public async Task<SuccessOrError<NoticeAnswer>> Handle(ConfirmEmailNoticesRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Token)) return SocialError.NoticeNotFound;

            var follow = await _follows.FindByNoticeToken(request.Token);
            // after "stop" the link of the confirmation is dead: a stopped follower is not signed up again by an old mail
            if (follow == null || follow.Notice == NoticeStatus.Off) return SocialError.NoticeNotFound;

            if (follow.Notice == NoticeStatus.Pending)
                await _follows.SetNotice(follow.FollowId, NoticeStatus.On, follow.NoticeEmail, follow.NoticeToken);

            var creator = await _creators.FindById(follow.CreatorId);
            return new NoticeAnswer(creator?.DisplayName ?? "", NoticeStatus.On);
        }
    }

    public class StopEmailNoticesByTokenUseCase
        : IRequestHandler<StopEmailNoticesByTokenRequest, SuccessOrError<NoticeAnswer>>
    {
        private readonly IFollowRepo _follows;
        private readonly ICreatorRepo _creators;

        public StopEmailNoticesByTokenUseCase(IFollowRepo follows, ICreatorRepo creators)
        {
            _follows = follows;
            _creators = creators;
        }

        public async Task<SuccessOrError<NoticeAnswer>> Handle(StopEmailNoticesByTokenRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Token)) return SocialError.NoticeNotFound;

            var follow = await _follows.FindByNoticeToken(request.Token);
            if (follow == null) return SocialError.NoticeNotFound;

            // the token stays: clicking the link again is the same answer
            if (follow.Notice != NoticeStatus.Off)
                await _follows.SetNotice(follow.FollowId, NoticeStatus.Off, null, follow.NoticeToken);

            var creator = await _creators.FindById(follow.CreatorId);
            return new NoticeAnswer(creator?.DisplayName ?? "", NoticeStatus.Off);
        }
    }

    public class SendFollowerNoticesUseCase : IRequestHandler<SendFollowerNoticesRequest, SuccessOrError<int>>
    {
        /// <summary>Only what is new is announced: this old, a post is not announced any more (and a fresh start of the server does not mail the past)</summary>
        private static readonly TimeSpan NewFor = TimeSpan.FromHours(24);

        private readonly IVideoRepo _videos;
        private readonly ISessionRepo _sessions;
        private readonly ICreatorRepo _creators;
        private readonly IFollowRepo _follows;
        private readonly IEmailSender _email;
        private readonly RemindersOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<SendFollowerNoticesUseCase> _logger;

        public SendFollowerNoticesUseCase(IVideoRepo videos, ISessionRepo sessions, ICreatorRepo creators,
            IFollowRepo follows, IEmailSender email, IOptions<RemindersOptions> options, TimeProvider timeProvider,
            ILogger<SendFollowerNoticesUseCase> logger)
        {
            _videos = videos;
            _sessions = sessions;
            _creators = creators;
            _follows = follows;
            _email = email;
            _options = options.Value;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<SuccessOrError<int>> Handle(SendFollowerNoticesRequest request,
            CancellationToken cancellationToken)
        {
            // nothing is used up while mails cannot be sent: the notice goes out once they can
            if (!_options.Enabled || !_email.IsConfigured) return 0;

            var now = _timeProvider.GetUtcNow();
            var sent = 0;

            foreach (var video in await _videos.FindForFollowerNotice(now - NewFor, 50))
            {
                // only one instance tells it, and only once
                if (!await _videos.TryClaimFollowerNotice(video.VideoId, now)) continue;

                sent += await Tell(video.CreatorId, now,
                    name => $"posted a new video: {video.Title}",
                    $"{_options.LinkBaseUrl.TrimEnd('/')}/v/{Uri.EscapeDataString(video.VideoId)}", "Watch it",
                    cancellationToken);
            }

            foreach (var session in await _sessions.FindForFollowerNotice(now - NewFor, 50))
            {
                if (!await _sessions.TryClaimFollowerNotice(session.SessionId, now)) continue;

                // a session in the past is not news
                if (session.StartsAt != null && session.StartsAt < now) continue;

                var creator = await _creators.FindById(session.CreatorId);
                var when = session.StartsAt == null
                    ? ""
                    : " on " + ReminderMail.FormatTime(session.StartsAt.Value, creator?.Country ?? "NG");
                sent += await Tell(session.CreatorId, now, name => $"has a new session: {session.Title}{when}",
                    $"{_options.LinkBaseUrl.TrimEnd('/')}/s/{Uri.EscapeDataString(session.SessionId)}", "See the session",
                    cancellationToken);
            }

            return sent;
        }

        private async Task<int> Tell(string creatorId, DateTimeOffset now, Func<string, string> headline, string link,
            string linkLabel, CancellationToken cancellationToken)
        {
            var creator = await _creators.FindById(creatorId);
            if (creator == null) return 0;

            var recipients = await _follows.FindNoticeRecipients(creatorId,
                now.AddHours(-Math.Max(0, _options.FollowerNoticeCooldownHours)),
                Math.Max(1, _options.FollowerNoticeMaxRecipients));

            var delivered = new List<string>();
            foreach (var follow in recipients.Where(x => !string.IsNullOrWhiteSpace(x.NoticeEmail) &&
                                                          !string.IsNullOrWhiteSpace(x.NoticeToken)))
            {
                var mail = NoticeMail.News(creator.DisplayName, headline(creator.DisplayName), link, linkLabel,
                    NoticeMail.StopLink(_options, follow.NoticeToken!));
                try
                {
                    await _email.Send(follow.NoticeEmail!, mail.Subject, mail.Html, mail.Text, cancellationToken);
                    delivered.Add(follow.FollowId);
                }
                catch (EmailException e)
                {
                    _logger.LogWarning(e, "A notice of creator {creatorId} could not be sent to one follower", creatorId);
                }
            }

            if (delivered.Count > 0) await _follows.MarkNoticed(delivered, now);
            return delivered.Count;
        }
    }
}
