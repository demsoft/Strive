using System;
using MediatR;
using Strive.Core.Interfaces;
using Strive.Core.Services.Payments.Sessions;

namespace Strive.Core.Services.Payments.Requests
{
    /// <summary>A creator publishes a session: a room is made for it and tickets can be bought</summary>
    public record CreateSessionRequest(string UserId, string Title, string? Description, string Category,
        long PriceMinor, DateTimeOffset? StartsAt) : IRequest<SuccessOrError<PaidSession>>;

    /// <summary>Starts the payment of a ticket. The fan is sent to the returned address to pay.</summary>
    /// <param name="CallbackUrl">Where the provider sends the person back after paying</param>
    public record StartCheckoutRequest(string UserId, string SessionId, string Email, string CallbackUrl)
        : IRequest<SuccessOrError<CheckoutResult>>;

    /// <param name="AuthorizationUrl">Null if the person has a ticket already</param>
    public record CheckoutResult(string? AuthorizationUrl, string? Reference, bool AlreadyHasTicket);

    /// <summary>
    ///     Finds out what happened to a payment (asks the provider) and, if it was paid, settles the order and grants the
    ///     ticket. Safe to call any number of times, from the browser and from the notification of the provider.
    /// </summary>
    public record SettleOrderRequest(string Reference) : IRequest<SuccessOrError<TicketOrder>>;

    /// <summary>Whether a participant may join the room: only the host and ticket holders may join a paid session</summary>
    public record CheckSessionAccessRequest(Participant Participant) : IRequest<SuccessOrError<Unit>>;

    public record CancelSessionRequest(string UserId, string SessionId) : IRequest<SuccessOrError<Unit>>;
}
