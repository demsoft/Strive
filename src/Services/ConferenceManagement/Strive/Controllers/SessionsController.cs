using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Strive.Core.Services.ConferenceControl.Gateways;
using Strive.Core.Services.Payments;
using Strive.Core.Services.Payments.Gateways;
using Strive.Core.Services.Payments.Requests;
using Strive.Core.Services.Payments.Sessions;
using Strive.Core.Services.Payments.UseCases;
using Strive.Core.Services.Videos;
using Strive.Presenters;
using Strive.Extensions;
using Strive.Models.Response;

namespace Strive.Controllers
{
    /// <summary>Paid sessions: a creator publishes one, fans buy a ticket and get into the room.</summary>
    [ApiController]
    public class SessionsController : Controller
    {
        private readonly IMediator _mediator;
        private readonly ISessionRepo _sessions;
        private readonly ICreatorRepo _creators;
        private readonly IEntitlementRepo _entitlements;
        private readonly IOrderRepo _orders;
        private readonly IPassRepo _passes;
        private readonly IPassSubscriptionRepo _subscriptions;
        private readonly IOpenConferenceRepository _openConferences;
        private readonly IVideoRepo _videoRepo;
        private readonly Strive.Core.Services.Social.IFollowRepo _follows;
        private readonly EngagementPresenter _engagement;
        private readonly Strive.Core.Services.Reminders.RemindersOptions _reminders;
        private readonly Strive.Core.Services.Reminders.IEmailSender _emailSender;
        private readonly VideoPresenter _videoPresenter;
        private readonly VideoAccess _videoAccess;
        private readonly PaymentsOptions _paymentsOptions;
        private readonly IConfiguration _configuration;

        public SessionsController(IMediator mediator, ISessionRepo sessions, ICreatorRepo creators,
            IEntitlementRepo entitlements, IOrderRepo orders, IPassRepo passes, IPassSubscriptionRepo subscriptions,
            IOpenConferenceRepository openConferences, IVideoRepo videoRepo, VideoPresenter videoPresenter,
            VideoAccess videoAccess, Strive.Core.Services.Social.IFollowRepo follows, EngagementPresenter engagement,
            Microsoft.Extensions.Options.IOptions<Strive.Core.Services.Reminders.RemindersOptions> reminders,
            Strive.Core.Services.Reminders.IEmailSender emailSender,
            Microsoft.Extensions.Options.IOptions<PaymentsOptions> paymentsOptions, IConfiguration configuration)
        {
            _openConferences = openConferences;
            _videoRepo = videoRepo;
            _follows = follows;
            _engagement = engagement;
            _reminders = reminders.Value;
            _emailSender = emailSender;
            _videoPresenter = videoPresenter;
            _videoAccess = videoAccess;
            _passes = passes;
            _subscriptions = subscriptions;
            _paymentsOptions = paymentsOptions.Value;
            _orders = orders;
            _mediator = mediator;
            _sessions = sessions;
            _creators = creators;
            _entitlements = entitlements;
            _configuration = configuration;
        }

        private string? UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // POST v1/sessions
        [HttpPost("v1/sessions")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<SessionDto>> Create([FromBody] CreateSessionDto dto)
        {
            if (UserId == null) return Forbid();

            var result = await _mediator.Send(new CreateSessionRequest(UserId, dto.Title, dto.Description,
                dto.Category, dto.PriceMinor, dto.StartsAt, dto.DurationMinutes));
            if (!result.Success) return result.ToActionResult();

            return SessionDto.From(result.Response!);
        }

        // GET v1/sessions/mine
        [HttpGet("v1/sessions/mine")]
        [Authorize]
        public async Task<ActionResult<SessionDto[]>> GetMine()
        {
            if (UserId == null) return Forbid();

            var creator = await _creators.FindByUserId(UserId);
            if (creator == null) return Array.Empty<SessionDto>();

            Response.Headers.CacheControl = "no-store";
            var result = new List<SessionDto>();
            foreach (var session in await _sessions.FindOfCreator(creator.CreatorId, 100))
            {
                // after a cancellation the host sees how many tickets were given back and how many are still to do
                var orders = session.Status == SessionStatus.Cancelled
                    ? await _orders.FindPaidOfSession(session.SessionId)
                    : new List<TicketOrder>();
                result.Add(SessionDto.From(session, orders.Count(x => x.Status == OrderStatus.Refunded),
                    orders.Count(x => x.Status == OrderStatus.Paid)));
            }

            return result.ToArray();
        }

        // GET v1/creators/me/earnings
        /// <summary>What the creator earned. Only the creator sees it.</summary>
        [HttpGet("v1/creators/me/earnings")]
        [Authorize]
        public async Task<ActionResult<EarningsDto>> GetEarnings()
        {
            if (UserId == null) return Forbid();

            var creator = await _creators.FindByUserId(UserId);
            if (creator == null) return PaymentsError.CreatorNotFound.ToActionResult();

            var orders = await _orders.FindPaidOfCreator(creator.CreatorId, 5000);
            var titles = new Dictionary<string, string>();
            foreach (var sessionId in orders.Where(x => x.Kind == OrderKind.Ticket).Select(x => x.SessionId).Distinct())
            {
                var session = await _sessions.FindById(sessionId);
                if (session != null) titles[sessionId] = session.Title;
            }

            var videoTitles = new Dictionary<string, string>();
            foreach (var id in orders.Where(x => x.Kind == OrderKind.Video && x.VideoId != null).Select(x => x.VideoId!)
                         .Distinct())
            {
                var video = await _videoRepo.FindById(id);
                if (video != null) videoTitles[id] = video.Title;
            }

            var report = EarningsReport.Build(creator.CreatorId, orders, titles, videoTitles);
            Response.Headers.CacheControl = "no-store";
            return new EarningsDto(creator.Currency, report.Totals, report.Sessions, report.Recent, report.Videos);
        }

        // GET v1/sessions/discover
        /// <summary>What is on: published sessions that are not over, the ones that start first at the top</summary>
        [HttpGet("v1/sessions/discover")]
        [AllowAnonymous]
        public async Task<ActionResult<DiscoverSessionDto[]>> Discover()
        {
            // nothing lasts longer than the longest session: what started earlier than that is over
            var sessions = await _sessions.FindForSale(
                DateTimeOffset.UtcNow.AddMinutes(-SessionRules.MaxDurationMinutes), 60);

            var result = new List<DiscoverSessionDto>();
            foreach (var session in sessions.OrderBy(x => x.StartsAt ?? DateTimeOffset.MaxValue))
            {
                var creator = await _creators.FindById(session.CreatorId);
                if (creator == null) continue;

                var dto = await ToDiscoverDto(session, creator);
                if (dto != null) result.Add(dto);
            }

            // the answer depends on who asks
            Response.Headers.CacheControl = "no-store";
            return result.ToArray();
        }

        // GET v1/creators/{creatorId}/page
        /// <summary>The page of a creator, the one link to share: the name, the bio and what they have on</summary>
        [HttpGet("v1/creators/{creatorId}/page")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CreatorPageDto>> GetCreatorPage(string creatorId)
        {
            // the link has the id or the short name (an id has 32 characters, a name at most 30: they cannot be mixed up)
            var creator = await _creators.FindById(creatorId) ??
                          await _creators.FindBySlug(SlugRules.Normalize(creatorId));
            if (creator == null) return PaymentsError.SessionNotFound.ToActionResult();

            var sessions = await _sessions.FindForSaleOfCreator(creator.CreatorId,
                DateTimeOffset.UtcNow.AddMinutes(-SessionRules.MaxDurationMinutes), 60);
            var list = new List<DiscoverSessionDto>();
            foreach (var session in sessions.OrderBy(x => x.StartsAt ?? DateTimeOffset.MaxValue))
            {
                var dto = await ToDiscoverDto(session, creator);
                if (dto != null) list.Add(dto);
            }

            var passUntil = UserId == null
                ? null
                : await _passes.ActiveUntil(UserId, creator.CreatorId, DateTimeOffset.UtcNow);
            var renewing = UserId == null
                ? null
                : (await _subscriptions.FindActiveOfUser(UserId)).FirstOrDefault(x => x.CreatorId == creator.CreatorId);

            Response.Headers.CacheControl = "no-store";
            var following = UserId == null ? null : await _follows.Find(UserId, creator.CreatorId);
            var publicCreator = PublicCreatorDto.From(creator);
            var readyVideos = await _videoRepo.FindReadyOfCreator(creator.CreatorId, 24);
            var unlocked = await _videoAccess.Unlocked(readyVideos.ToList(), UserId);
            var videos = await _engagement.Apply(readyVideos
                .Select(x => _videoPresenter.ToDto(x, publicCreator, UserId != null && x.CreatorUserId == UserId,
                    unlocked.Contains(x.VideoId), false))
                .ToList(), UserId);
            return new CreatorPageDto(publicCreator, list.ToArray(), passUntil != null, passUntil,
                _paymentsOptions.AutoRenewEnabled, renewing?.NextChargeAt, videos,
                await _follows.CountOfCreator(creator.CreatorId), following != null, UserId != null && creator.UserId == UserId,
                following?.Notice.ToString().ToLowerInvariant() ?? "off",
                _reminders.Enabled && _emailSender.IsConfigured);
        }

        /// <summary>The session in a list; null if it is over (it was meant to end and its room is not open)</summary>
        private async Task<DiscoverSessionDto?> ToDiscoverDto(PaidSession session, CreatorProfile creator)
        {
            var live = await IsLive(session);
            if (!live && session.EndsAt < DateTimeOffset.UtcNow) return null;

            return new DiscoverSessionDto(session.SessionId, session.Title, session.Description, session.Category,
                session.PriceMinor, session.Currency, session.StartsAt, creator.DisplayName,
                UserId != null && session.CreatorUserId == UserId,
                UserId != null && await CanJoin(session), session.DurationMinutes, live, creator.CreatorId,
                creator.PhotoVersion);
        }

        /// <summary>Whether the host has opened the room and not closed it again</summary>
        private async Task<bool> IsLive(PaidSession session) =>
            session.Status == SessionStatus.Published && await _openConferences.IsOpen(session.ConferenceId);

        /// <summary>A ticket for the session, or a running pass of its host</summary>
        private async Task<bool> CanJoin(PaidSession session) =>
            await _entitlements.Holds(UserId!, session.ConferenceId) ||
            await _passes.ActiveUntil(UserId!, session.CreatorId, DateTimeOffset.UtcNow) != null;

        // GET v1/sessions/{sessionId}
        [HttpGet("v1/sessions/{sessionId}")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PublicSessionDto>> Get(string sessionId)
        {
            var session = await _sessions.FindById(sessionId);
            if (session == null) return PaymentsError.SessionNotFound.ToActionResult();

            var creator = await _creators.FindById(session.CreatorId);
            if (creator == null) return PaymentsError.SessionNotFound.ToActionResult();

            var isHost = UserId != null && session.CreatorUserId == UserId;
            var hasTicket = UserId != null && await CanJoin(session);

            // the answer depends on who asks: never cache it
            Response.Headers.CacheControl = "no-store";
            return new PublicSessionDto(session.SessionId, session.Title, session.Description, session.Category,
                session.PriceMinor, session.Currency, session.Status, session.StartsAt,
                PublicCreatorDto.From(creator), isHost, hasTicket,
                isHost || hasTicket ? session.ConferenceId : null, session.DurationMinutes, await IsLive(session));
        }

        // POST v1/sessions/{sessionId}/checkout
        [HttpPost("v1/sessions/{sessionId}/checkout")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CheckoutResultDto>> Checkout(string sessionId, [FromBody] CheckoutDto dto)
        {
            if (UserId == null) return Forbid();

            var result = await _mediator.Send(new StartCheckoutRequest(UserId, sessionId, dto.Email,
                CallbackUrl("s", sessionId)));
            if (!result.Success) return result.ToActionResult();

            Response.Headers.CacheControl = "no-store";
            return new CheckoutResultDto(result.Response!.AuthorizationUrl, result.Response.Reference,
                result.Response.AlreadyHasTicket);
        }

        // POST v1/creators/{creatorId}/pass/checkout
        [HttpPost("v1/creators/{creatorId}/pass/checkout")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CheckoutResultDto>> PassCheckout(string creatorId, [FromBody] CheckoutDto dto)
        {
            if (UserId == null) return Forbid();

            var result = await _mediator.Send(new StartPassCheckoutRequest(UserId, creatorId, dto.Email,
                CallbackUrl("u", creatorId), dto.AutoRenew));
            if (!result.Success) return result.ToActionResult();

            Response.Headers.CacheControl = "no-store";
            return new CheckoutResultDto(result.Response!.AuthorizationUrl, result.Response.Reference,
                result.Response.AlreadyHasTicket);
        }

        // GET v1/passes/mine
        [HttpGet("v1/passes/mine")]
        [Authorize]
        public async Task<ActionResult<PassDto[]>> GetMyPasses()
        {
            if (UserId == null) return Forbid();

            var result = new List<PassDto>();
            var renewing = (await _subscriptions.FindActiveOfUser(UserId)).ToList();
            // a person with two passes of one creator (bought early) is shown once, with the latest end
            foreach (var pass in (await _passes.FindActiveOfUser(UserId, DateTimeOffset.UtcNow))
                     .GroupBy(x => x.CreatorId).Select(g => g.OrderByDescending(x => x.ValidUntil).First()))
            {
                var creator = await _creators.FindById(pass.CreatorId);
                var subscription = renewing.FirstOrDefault(x => x.CreatorId == pass.CreatorId);
                result.Add(new PassDto(pass.CreatorId, creator?.DisplayName ?? "", pass.ValidUntil,
                    subscription?.SubscriptionId, subscription?.NextChargeAt, creator?.Slug));
            }

            Response.Headers.CacheControl = "no-store";
            return result.ToArray();
        }

        // POST v1/passes/subscriptions/{subscriptionId}/cancel
        [HttpPost("v1/passes/subscriptions/{subscriptionId}/cancel")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> CancelSubscription(string subscriptionId)
        {
            if (UserId == null) return Forbid();

            return (await _mediator.Send(new CancelPassSubscriptionRequest(UserId, subscriptionId))).ToActionResult();
        }

        // POST v1/sessions/{sessionId}/cancel
        [HttpPost("v1/sessions/{sessionId}/cancel")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> Cancel(string sessionId)
        {
            if (UserId == null) return Forbid();

            return (await _mediator.Send(new CancelSessionRequest(UserId, sessionId))).ToActionResult();
        }

        // GET v1/tickets/mine
        [HttpGet("v1/tickets/mine")]
        [Authorize]
        public async Task<ActionResult<TicketDto[]>> GetMyTickets()
        {
            if (UserId == null) return Forbid();

            var tickets = new List<TicketDto>();
            foreach (var entitlement in await _entitlements.FindOfUser(UserId, 100))
            {
                var session = await _sessions.FindById(entitlement.SessionId);
                if (session == null) continue;
                var creator = await _creators.FindById(session.CreatorId);
                var order = await _orders.FindById(entitlement.OrderId);
                tickets.Add(new TicketDto(session.SessionId, session.ConferenceId, session.Title, session.Category,
                    session.StartsAt, creator?.DisplayName ?? "", entitlement.GrantedAt, session.Status,
                    order?.Status == OrderStatus.Refunded, session.DurationMinutes, await IsLive(session), creator?.CreatorId,
                    creator?.PhotoVersion));
            }

            Response.Headers.CacheControl = "no-store";
            return tickets.ToArray();
        }

        private string CallbackUrl(string kind, string id) => ReturnUrls.For(Request, _configuration, kind, id);
    }
}
