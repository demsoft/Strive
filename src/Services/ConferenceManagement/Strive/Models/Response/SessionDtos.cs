using System;
using System.Linq;
using Strive.Core.Services.Payments;
using Strive.Core.Services.Payments.Sessions;

namespace Strive.Models.Response
{
    public record CreateSessionDto(string Title, string? Description, string Category, long PriceMinor,
        DateTimeOffset? StartsAt);

    /// <summary>A session as its host sees it</summary>
    public record SessionDto(string SessionId, string ConferenceId, string Title, string? Description,
        string Category, long PriceMinor, string Currency, SessionStatus Status, DateTimeOffset? StartsAt,
        DateTimeOffset CreatedAt, int Refunded = 0, int RefundsPending = 0)
    {
        public static SessionDto From(PaidSession session, int refunded = 0, int refundsPending = 0)
        {
            return new SessionDto(session.SessionId, session.ConferenceId, session.Title, session.Description,
                session.Category, session.PriceMinor, session.Currency, session.Status, session.StartsAt,
                session.CreatedAt, refunded, refundsPending);
        }
    }

    /// <summary>
    ///     What the page of a session shows to everybody. The room (conference id) is only given to the host and to people
    ///     with a ticket.
    /// </summary>
    public record PublicSessionDto(string SessionId, string Title, string? Description, string Category,
        long PriceMinor, string Currency, SessionStatus Status, DateTimeOffset? StartsAt, PublicCreatorDto Creator,
        bool IsHost, bool HasTicket, string? ConferenceId);

    /// <summary>A session in the list of what is on, for everybody to see (the room is not part of it)</summary>
    public record DiscoverSessionDto(string SessionId, string Title, string? Description, string Category,
        long PriceMinor, string Currency, DateTimeOffset? StartsAt, string CreatorName, bool IsHost, bool HasTicket);

    public record CheckoutDto(string Email);

    public record CheckoutResultDto(string? AuthorizationUrl, string? Reference, bool AlreadyHasTicket);

    /// <summary>The state of a payment, for the page the person comes back to after paying</summary>
    public record PaymentStatusDto(string Reference, string Status, string SessionId, bool HasTicket,
        string? ConferenceId, string? FailureReason);

    public record TicketDto(string SessionId, string ConferenceId, string Title, string Category,
        DateTimeOffset? StartsAt, string CreatorName, DateTimeOffset GrantedAt, SessionStatus Status,
        bool Refunded = false);
}
