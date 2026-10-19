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
        private readonly IConfiguration _configuration;

        public SessionsController(IMediator mediator, ISessionRepo sessions, ICreatorRepo creators,
            IEntitlementRepo entitlements, IConfiguration configuration)
        {
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
            return (await _sessions.FindOfCreator(creator.CreatorId, 100)).Select(SessionDto.From).ToArray();
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

                result.Add(new DiscoverSessionDto(session.SessionId, session.Title, session.Description,
                    session.Category, session.PriceMinor, session.Currency, session.StartsAt, creator.DisplayName,
                    UserId != null && session.CreatorUserId == UserId,
                    UserId != null && await _entitlements.Holds(UserId, session.ConferenceId)));
            }

            // the answer depends on who asks
            Response.Headers.CacheControl = "no-store";
            return result.ToArray();
        }

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
            var hasTicket = UserId != null && await _entitlements.Holds(UserId, session.ConferenceId);

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
                CallbackUrl(sessionId)));
            if (!result.Success) return result.ToActionResult();

            Response.Headers.CacheControl = "no-store";
            return new CheckoutResultDto(result.Response!.AuthorizationUrl, result.Response.Reference,
                result.Response.AlreadyHasTicket);
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
                tickets.Add(new TicketDto(session.SessionId, session.ConferenceId, session.Title, session.Category,
                    session.StartsAt, creator?.DisplayName ?? "", entitlement.GrantedAt, session.Status));
            }

            Response.Headers.CacheControl = "no-store";
            return tickets.ToArray();
        }

        /// <summary>
        ///     Where the provider sends the person back after paying: the page of the session on the address the person
        ///     came from (one installation has several addresses), if that is one of ours.
        /// </summary>
        private string CallbackUrl(string sessionId)
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

            return $"{home}/s/{Uri.EscapeDataString(sessionId)}";
        }
    }
}
