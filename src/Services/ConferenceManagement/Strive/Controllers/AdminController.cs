using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Strive.Admin;
using Strive.Core.Services.Payments;
using Strive.Core.Services.Payments.Gateways;
using Strive.Core.Services.Payments.Sessions;
using Strive.Extensions;
using Strive.Models.Response;

namespace Strive.Controllers
{
    /// <summary>The overview for server administrators: is the server full, what runs, who uses it.</summary>
    [ApiController]
    [Route("v1/admin")]
    [Authorize(Policy = AdminOptions.Policy)]
    public class AdminController : ControllerBase
    {
        public const int MaxHistoryHours = 7 * 24;
        private const int MaxPoints = 400;

        private readonly IAdminMetricsStore _history;
        private readonly AdminOverviewService _overview;
        private readonly IOrderRepo _orders;
        private readonly ISessionRepo _sessions;
        private readonly ICreatorRepo _creators;
        private readonly IPassSubscriptionRepo _subscriptions;
        private readonly Strive.Core.Services.Videos.IVideoRepo _videoRepo;
        private readonly Strive.Core.Services.Videos.IVideoReportRepo _videoReports;
        private readonly Strive.Core.Services.Videos.IVideoEntitlementRepo _videoEntitlements;
        private readonly Strive.Presenters.VideoPresenter _videoPresenter;
        private readonly MediatR.IMediator _mediator;
        private readonly PaymentsOptions _payments;

        public AdminController(AdminOverviewService overview, IAdminMetricsStore history, IOrderRepo orders,
            ISessionRepo sessions, ICreatorRepo creators, IPassSubscriptionRepo subscriptions,
            Strive.Core.Services.Videos.IVideoRepo videoRepo, Strive.Core.Services.Videos.IVideoReportRepo videoReports,
            Strive.Core.Services.Videos.IVideoEntitlementRepo videoEntitlements, Strive.Presenters.VideoPresenter videoPresenter,
            MediatR.IMediator mediator,
            IOptions<PaymentsOptions> payments)
        {
            _overview = overview;
            _history = history;
            _orders = orders;
            _sessions = sessions;
            _creators = creators;
            _subscriptions = subscriptions;
            _videoRepo = videoRepo;
            _videoReports = videoReports;
            _videoEntitlements = videoEntitlements;
            _videoPresenter = videoPresenter;
            _mediator = mediator;
            _payments = payments.Value;
        }

        /// <summary>Videos that people reported, and the ones that are hidden until somebody looks at them</summary>
        [HttpGet("videos/reports")]
        public async Task<ActionResult<AdminVideoReportDto[]>> GetVideoReports()
        {
            var open = await _videoReports.FindOpen(500);
            var moderated = await _videoRepo.FindModerated(50);
            var hidden = moderated.Where(x => x.Moderation == Strive.Core.Services.Videos.VideoModeration.Hidden)
                .ToList();

            // what a moderator already removed needs no more looking at, unless a refund did not work
            var stuck = new Dictionary<string, int>();
            foreach (var removed in moderated.Where(x =>
                         x.Moderation == Strive.Core.Services.Videos.VideoModeration.Removed))
            {
                var pending = (await _orders.FindPaidOfVideo(removed.VideoId)).Count(x => x.Status == OrderStatus.Paid);
                if (pending > 0) stuck[removed.VideoId] = pending;
            }

            var videos = (await _videoRepo.FindByIds(open.Select(x => x.VideoId).Concat(hidden.Select(x => x.VideoId))
                    .Concat(stuck.Keys)))
                .Where(x => x.Moderation != Strive.Core.Services.Videos.VideoModeration.Removed ||
                            stuck.ContainsKey(x.VideoId)).ToList();
            var names = await _creators.FindNames(videos.Select(x => x.CreatorId));

            var result = new List<AdminVideoReportDto>();
            foreach (var video in videos)
            {
                var reports = open.Where(x => x.VideoId == video.VideoId).ToList();
                var shown = _videoPresenter.ToDto(video, null, false, true, true);
                result.Add(new AdminVideoReportDto(video.VideoId, video.Title, names.GetValueOrDefault(video.CreatorId, ""),
                    video.Moderation.ToString().ToLowerInvariant(), video.PriceMinor,
                    await _videoEntitlements.CountOfVideo(video.VideoId), reports.Count,
                    reports.GroupBy(x => x.Reason.ToString().ToLowerInvariant()).ToDictionary(g => g.Key, g => g.Count()),
                    reports.Where(x => !string.IsNullOrEmpty(x.Details)).Take(5)
                        .Select(x => new ReportDetailDto(x.Reason.ToString().ToLowerInvariant(), x.Details, x.CreatedAt))
                        .ToList(),
                    reports.Count == 0 ? null : reports.Max(x => x.CreatedAt), shown.PosterUrl, shown.PlaybackUrl,
                    stuck.GetValueOrDefault(video.VideoId)));
            }

            Response.Headers.CacheControl = "no-store";
            return result.OrderByDescending(x => x.OpenReports).ThenByDescending(x => x.LatestReportAt).ToArray();
        }

        /// <summary>The video is fine: shown again, the reports are put aside</summary>
        [HttpPost("videos/{videoId}/keep")]
        public async Task<ActionResult> KeepVideo(string videoId)
        {
            var result = await _mediator.Send(new Strive.Core.Services.Videos.ModerateVideoRequest(videoId,
                Strive.Core.Services.Videos.ModerationDecision.Keep, null));
            // nothing of the video itself goes back, the page asks for the list again
            return result.Success ? NoContent() : result.ToActionResult();
        }

        /// <summary>The video goes: its files are deleted and the creator is told why</summary>
        [HttpPost("videos/{videoId}/remove")]
        public async Task<ActionResult<RemovedVideoDto>> RemoveVideo(string videoId, [FromBody] ModerateVideoDto dto)
        {
            var result = await _mediator.Send(new Strive.Core.Services.Videos.ModerateVideoRequest(videoId,
                Strive.Core.Services.Videos.ModerationDecision.Remove, dto.Note));
            if (!result.Success) return result.ToActionResult();

            // the buyers were refunded on the way: say how it went (a refund that failed is tried again by removing again)
            var orders = await _orders.FindPaidOfVideo(videoId);
            return new RemovedVideoDto(orders.Count(x => x.Status == OrderStatus.Refunded),
                orders.Count(x => x.Status == OrderStatus.Paid));
        }

        /// <summary>Money: what was sold and paid, and what needs a person (refunds that failed, renewals that fail)</summary>
        [HttpGet("payments")]
        public async Task<ActionResult<AdminPaymentsDto>> GetPayments()
        {
            var now = DateTimeOffset.UtcNow;
            var monthOrders = await _orders.FindPaidSince(now.AddDays(-30), 20000);
            var recent = await _orders.FindRecent(30);
            var refundProblems = await _orders.FindWithRefundFailure(50);
            var subscriptionProblems = await _subscriptions.FindWithProblems(now.AddDays(-30), 50);

            var all = recent.Concat(refundProblems).ToList();
            var names = await _creators.FindNames(all.Select(x => PaymentsOverview.CreatorOf(x))
                .Concat(subscriptionProblems.Select(x => (string?) x.CreatorId)).Where(x => x != null).Select(x => x!));
            var titles = new Dictionary<string, string>();
            foreach (var id in all.Where(x => x.Kind == OrderKind.Ticket).Select(x => x.SessionId).Distinct())
            {
                var session = await _sessions.FindById(id);
                if (session != null) titles[id] = session.Title;
            }

            var videoTitles = new Dictionary<string, string>();
            foreach (var id in all.Where(x => x.Kind == OrderKind.Video && x.VideoId != null).Select(x => x.VideoId!).Distinct())
            {
                var video = await _videoRepo.FindById(id);
                if (video != null) videoTitles[id] = video.Title;
            }

            Response.Headers.CacheControl = "no-store";
            return new AdminPaymentsDto(_payments.Currency, _payments.Enabled, _payments.AutoRenewEnabled,
                await _creators.Count(), await _subscriptions.CountActive(),
                PaymentsOverview.Period(monthOrders, now, 7), PaymentsOverview.Period(monthOrders, now, 30),
                recent.Select(x => PaymentsOverview.Row(x, names, titles, videoTitles)).ToList(),
                refundProblems.Select(x => PaymentsOverview.Refund(x, names, titles)).ToList(),
                subscriptionProblems.Select(x => PaymentsOverview.Subscription(x, names)).ToList());
        }

        [HttpGet("overview")]
        public async Task<ActionResult<AdminOverview>> GetOverview(CancellationToken cancellationToken)
        {
            Response.Headers.CacheControl = "no-store";
            return await _overview.GetOverviewAsync(cancellationToken);
        }

        [HttpGet("history")]
        public async Task<ActionResult<object>> GetHistory([FromQuery] int hours = 24)
        {
            hours = Math.Clamp(hours, 1, MaxHistoryHours);
            var samples = await _history.GetAsync(DateTime.UtcNow.AddHours(-hours), MaxPoints);

            Response.Headers.CacheControl = "no-store";
            return new
            {
                Hours = hours,
                Samples = samples.Select(x => new
                {
                    T = DateTime.SpecifyKind(x.T, DateTimeKind.Utc), x.Participants, x.Conferences, x.MaxWorkerCpu,
                    x.AverageWorkerCpu, x.MemoryUsedPercent, x.LoadPerCore, x.Alerts,
                }),
            };
        }
    }
}
