using System;
using MediatR;
using Strive.Core.Interfaces;
using Strive.Core.Services.Payments.Sessions;

namespace Strive.Core.Services.Payments.Requests
{
    /// <summary>A creator publishes a session: a room is made for it and tickets can be bought</summary>
    public record CreateSessionRequest(string UserId, string Title, string? Description, string Category,
        long PriceMinor, DateTimeOffset? StartsAt, int? DurationMinutes = null) : IRequest<SuccessOrError<PaidSession>>;

    /// <summary>Starts the payment of a ticket. The fan is sent to the returned address to pay.</summary>
    /// <param name="CallbackUrl">Where the provider sends the person back after paying</param>
    public record StartCheckoutRequest(string UserId, string SessionId, string Email, string CallbackUrl)
        : IRequest<SuccessOrError<CheckoutResult>>;

    /// <summary>Starts the payment of a pass: all sessions of a creator for <see cref="PassRules.Days" /> days</summary>
    /// <param name="AutoRenew">The person agrees that the pass renews itself (the card is charged again)</param>
    public record StartPassCheckoutRequest(string UserId, string CreatorId, string Email, string CallbackUrl,
        bool AutoRenew = false) : IRequest<SuccessOrError<CheckoutResult>>;

    /// <summary>The person stops a pass from renewing. The pass they paid for runs until its end.</summary>
    public record CancelPassSubscriptionRequest(string UserId, string SubscriptionId) : IRequest<SuccessOrError<Unit>>;

    /// <param name="Renewed">Passes renewed</param>
    /// <param name="Failed">Renewals that did not work (they are tried again, or the subscription ends)</param>
    /// <param name="Ended">Subscriptions that ended</param>
    /// <param name="Notified">People told that their pass renews soon</param>
    public record RenewalSummary(int Renewed, int Failed, int Ended, int Notified);

    /// <summary>Charges the renewals that are due and tells people about the ones that are coming</summary>
    public record RenewPassSubscriptionsRequest : IRequest<SuccessOrError<RenewalSummary>>;

    /// <summary>A creator sets their photo, or removes it (null)</summary>
    public record SetCreatorPhotoRequest(string UserId, byte[]? Data) : IRequest<SuccessOrError<CreatorProfile>>;

    /// <summary>A creator chooses the short name in the link of their page, or goes back to the id (null)</summary>
    public record SetSlugRequest(string UserId, string? Slug) : IRequest<SuccessOrError<CreatorProfile>>;

    /// <summary>A creator sets what a pass costs, or stops selling it (null)</summary>
    public record SetPassPriceRequest(string UserId, long? PriceMinor) : IRequest<SuccessOrError<CreatorProfile>>;

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
