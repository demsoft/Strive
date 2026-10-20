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
using Strive.Core.Services.Payments;
using Strive.Core.Services.Payments.Gateways;
using Strive.Core.Services.Payments.Requests;
using Strive.Core.Services.Payments.Sessions;
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
        private readonly IConfiguration _configuration;

        public SessionsController(IMediator mediator, ISessionRepo sessions, ICreatorRepo creators,
            IEntitlementRepo entitlements, IOrderRepo orders, IPassRepo passes, IConfiguration configuration)
        {
            _passes = passes;
            _orders = orders;
            _mediator = mediator;
            _sessions = sessions;
            _creators = creators;
            _entitlements = entitlements;
            _configuration = configuration;
        }

        private const int SessionOverAfterHours = 8;

        private string? UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // POST v1/sessions
        [HttpPost("v1/sessions")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<SessionDto>> Create([FromBody] CreateSessionDto dto)
        {
            if (UserId == null) return Forbid();

            var result = await _mediator.Send(new CreateSessionRequest(UserId, dto.Title, dto.Description,
                dto.Category, dto.PriceMinor, dto.StartsAt));
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

        // GET v1/sessions/discover
        /// <summary>What is on: published sessions that are not over, the ones that start first at the top</summary>
        [HttpGet("v1/sessions/discover")]
        [AllowAnonymous]
        public async Task<ActionResult<DiscoverSessionDto[]>> Discover()
        {
            // sessions have no end time yet: one that started this long ago is over
            var sessions = await _sessions.FindForSale(DateTimeOffset.UtcNow.AddHours(-SessionOverAfterHours), 60);

            var result = new List<DiscoverSessionDto>();
            foreach (var session in sessions.OrderBy(x => x.StartsAt ?? DateTimeOffset.MaxValue))
            {
                var creator = await _creators.FindById(session.CreatorId);
                if (creator == null) continue;

                result.Add(await ToDiscoverDto(session, creator));
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
            var creator = await _creators.FindById(creatorId);
            if (creator == null) return PaymentsError.SessionNotFound.ToActionResult();

            var sessions = await _sessions.FindForSaleOfCreator(creator.CreatorId,
                DateTimeOffset.UtcNow.AddHours(-SessionOverAfterHours), 60);
            var list = new List<DiscoverSessionDto>();
            foreach (var session in sessions.OrderBy(x => x.StartsAt ?? DateTimeOffset.MaxValue))
                list.Add(await ToDiscoverDto(session, creator));

            var passUntil = UserId == null
                ? null
                : await _passes.ActiveUntil(UserId, creator.CreatorId, DateTimeOffset.UtcNow);

            Response.Headers.CacheControl = "no-store";
            return new CreatorPageDto(PublicCreatorDto.From(creator), list.ToArray(), passUntil != null, passUntil);
        }

        private async Task<DiscoverSessionDto> ToDiscoverDto(PaidSession session, CreatorProfile creator) =>
            new(session.SessionId, session.Title, session.Description, session.Category, session.PriceMinor,
                session.Currency, session.StartsAt, creator.DisplayName,
                UserId != null && session.CreatorUserId == UserId,
                UserId != null && await CanJoin(session));

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
                isHost || hasTicket ? session.ConferenceId : null);
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
                CallbackUrl("u", creatorId)));
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
            // a person with two passes of one creator (bought early) is shown once, with the latest end
            foreach (var pass in (await _passes.FindActiveOfUser(UserId, DateTimeOffset.UtcNow))
                     .GroupBy(x => x.CreatorId).Select(g => g.OrderByDescending(x => x.ValidUntil).First()))
            {
                var creator = await _creators.FindById(pass.CreatorId);
                result.Add(new PassDto(pass.CreatorId, creator?.DisplayName ?? "", pass.ValidUntil));
            }

            Response.Headers.CacheControl = "no-store";
            return result.ToArray();
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
                    order?.Status == OrderStatus.Refunded));
            }

            Response.Headers.CacheControl = "no-store";
            return tickets.ToArray();
        }

        /// <summary>
        ///     Where the provider sends the person back after paying: the page of the session on the address the person
        ///     came from (one installation has several addresses), if that is one of ours.
        /// </summary>
        private string CallbackUrl(string kind, string id)
        {
            var allowed = (_configuration["Cors:AllowedOrigins"] ?? "")
                .Split(new[] {',', ';', ' '}, StringSplitOptions.RemoveEmptyEntries)
                .Concat((_configuration["App:ExtraHosts"] ?? "")
                    .Split(new[] {',', ';', ' '}, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.StartsWith("http") ? x : "https://" + x))
                .Select(x => x.TrimEnd('/'))
                .ToList();

            var origin = Request.Headers.Origin.ToString().TrimEnd('/');
            var home = allowed.Contains(origin, StringComparer.OrdinalIgnoreCase)
                ? origin
                : allowed.FirstOrDefault() ?? _configuration["App:FrontendUrl"]?.TrimEnd('/') ?? "";

            return $"{home}/{kind}/{Uri.EscapeDataString(id)}";
        }
    }
}
