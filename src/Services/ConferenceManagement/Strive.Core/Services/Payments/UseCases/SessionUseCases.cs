using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Strive.Core.Domain.Entities;
using Strive.Core.Interfaces;
using Strive.Core.Services.ConferenceManagement;
using Strive.Core.Services.ConferenceManagement.Requests;
using Strive.Core.Services.Permissions.Options;
using Strive.Core.Services.Scenes;
using Strive.Core.Services.Payments.Gateways;
using Strive.Core.Services.Payments.Requests;
using Strive.Core.Services.Payments.Sessions;
using Strive.Core.Services.Reminders;
using Strive.Core.Services.Videos;

namespace Strive.Core.Services.Payments.UseCases
{
    public static class SessionRules
    {
        /// <summary>₦100: below that the fees of the payment would be most of it</summary>
        public const long MinPriceMinor = 10_000;

        /// <summary>₦1,000,000</summary>
        public const long MaxPriceMinor = 100_000_000;

        public const int MinDurationMinutes = 15;
        public const int MaxDurationMinutes = 480;
        public const int DefaultDurationMinutes = 60;

        public const int MinTitleLength = 3;
        public const int MaxTitleLength = 100;
        public const int MaxDescriptionLength = 2000;

        private static readonly Regex Email = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        public static bool IsValidEmail(string? email) => email is {Length: <= 254} && Email.IsMatch(email);

        /// <summary>A short code for the link: easy to read out and to type, no look-alike letters</summary>
        public static string NewSessionId()
        {
            const string alphabet = "abcdefghjkmnpqrstuvwxyz23456789";
            var bytes = RandomNumberGenerator.GetBytes(8);
            return new string(bytes.Select(b => alphabet[b % alphabet.Length]).ToArray());
        }

        public static string NewReference() => "gbd_" + Guid.NewGuid().ToString("N");
    }

    public class CreateSessionUseCase : IRequestHandler<CreateSessionRequest, SuccessOrError<PaidSession>>
    {
        private readonly IMediator _mediator;
        private readonly ICreatorRepo _creators;
        private readonly ISessionRepo _sessions;
        private readonly PaymentsOptions _options;
        private readonly DefaultPermissionOptions _defaultPermissions;
        private readonly TimeProvider _timeProvider;

        public CreateSessionUseCase(IMediator mediator, ICreatorRepo creators, ISessionRepo sessions,
            IOptions<PaymentsOptions> options, IOptions<DefaultPermissionOptions> defaultPermissions,
            TimeProvider timeProvider)
        {
            _mediator = mediator;
            _creators = creators;
            _sessions = sessions;
            _options = options.Value;
            _defaultPermissions = defaultPermissions.Value;
            _timeProvider = timeProvider;
        }

        public async Task<SuccessOrError<PaidSession>> Handle(CreateSessionRequest request,
            CancellationToken cancellationToken)
        {
            if (!_options.Enabled) return PaymentsError.NotEnabled;

            var creator = await _creators.FindByUserId(request.UserId);
            if (creator == null || !creator.CanSell) return PaymentsError.CreatorCannotSell;

            var title = request.Title?.Trim() ?? "";
            if (title.Length is < SessionRules.MinTitleLength or > SessionRules.MaxTitleLength)
                return PaymentsError.InvalidSession(
                    $"The title must have {SessionRules.MinTitleLength} to {SessionRules.MaxTitleLength} characters.");

            var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            if (description is {Length: > SessionRules.MaxDescriptionLength})
                return PaymentsError.InvalidSession(
                    $"The description can have {SessionRules.MaxDescriptionLength} characters at most.");

            if (!SessionCategories.All.Contains(request.Category))
                return PaymentsError.InvalidSession("Choose a category.");

            if (request.PriceMinor is < SessionRules.MinPriceMinor or > SessionRules.MaxPriceMinor)
                return PaymentsError.InvalidSession(
                    $"The price must be between {SessionRules.MinPriceMinor / 100} and {SessionRules.MaxPriceMinor / 100} {creator.Currency}.");

            var duration = request.DurationMinutes ?? SessionRules.DefaultDurationMinutes;
            if (duration is < SessionRules.MinDurationMinutes or > SessionRules.MaxDurationMinutes)
                return PaymentsError.InvalidSession(
                    $"A session lasts {SessionRules.MinDurationMinutes} to {SessionRules.MaxDurationMinutes} minutes.");

            // the room of the session: the creator is its moderator
            var data = new ConferenceData
            {
                Configuration = new ConferenceConfiguration
                {
                    Name = title,
                    Moderators = new[] {request.UserId}.ToList(),
                    // a stage: the person who speaks is shown large, the audience without a camera is not shown as
                    // empty tiles
                    Scenes = new SceneOptions
                    {
                        DefaultScene = SceneOptions.BasicSceneType.ActiveSpeaker,
                        HideParticipantsWithoutWebcam = true,
                    },
                },
                Permissions = _defaultPermissions.Default.ToDictionary(x => x.Key,
                    x => x.Value.ToDictionary(y => y.Key, y => y.Value)),
            };
            var conferenceId = await _mediator.Send(new CreateConferenceRequest(data), cancellationToken);

            var session = new PaidSession(SessionRules.NewSessionId(), conferenceId, creator.CreatorId, creator.UserId,
                title, request.PriceMinor, creator.Currency, _timeProvider.GetUtcNow())
            {
                Description = description,
                Category = request.Category,
                StartsAt = request.StartsAt,
                DurationMinutes = duration,
                Shares = new[] {new RevenueShare(creator.CreatorId, creator.ProviderSubaccountCode!, 100)}.ToList(),
            };
            await _sessions.Create(session);
            return session;
        }
    }

    public class StartCheckoutUseCase : IRequestHandler<StartCheckoutRequest, SuccessOrError<CheckoutResult>>
    {
        private readonly IPaymentProvider _provider;
        private readonly ICreatorRepo _creators;
        private readonly ISessionRepo _sessions;
        private readonly IOrderRepo _orders;
        private readonly IEntitlementRepo _entitlements;
        private readonly IPassRepo _passes;
        private readonly PaymentsOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<StartCheckoutUseCase> _logger;

        public StartCheckoutUseCase(IPaymentProvider provider, ICreatorRepo creators, ISessionRepo sessions,
            IOrderRepo orders, IEntitlementRepo entitlements, IPassRepo passes, IOptions<PaymentsOptions> options,
            TimeProvider timeProvider, ILogger<StartCheckoutUseCase> logger)
        {
            _passes = passes;
            _provider = provider;
            _creators = creators;
            _sessions = sessions;
            _orders = orders;
            _entitlements = entitlements;
            _options = options.Value;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<SuccessOrError<CheckoutResult>> Handle(StartCheckoutRequest request,
            CancellationToken cancellationToken)
        {
            if (!_options.Enabled) return PaymentsError.NotEnabled;
            if (!SessionRules.IsValidEmail(request.Email)) return PaymentsError.InvalidEmail;

            var session = await _sessions.FindById(request.SessionId);
            if (session == null) return PaymentsError.SessionNotFound;
            if (session.Status == SessionStatus.Cancelled) return PaymentsError.SessionCancelled;
            if (session.CreatorUserId == request.UserId) return PaymentsError.HostCannotBuy;

            // a person who has the ticket does not pay twice
            if (await _entitlements.Holds(request.UserId, session.ConferenceId))
                return new CheckoutResult(null, null, true);

            // a running pass of the host covers the session
            if (await _passes.ActiveUntil(request.UserId, session.CreatorId, _timeProvider.GetUtcNow()) != null)
                return new CheckoutResult(null, null, true);

            var creator = await _creators.FindById(session.CreatorId);
            if (creator == null || !creator.CanSell) return PaymentsError.CreatorCannotSell;
            if (session.Shares.Count != 1)
                return PaymentsError.InvalidSession("Sessions with several hosts cannot be paid yet.");

            var now = _timeProvider.GetUtcNow();
            var order = new TicketOrder(Guid.NewGuid().ToString("N"), SessionRules.NewReference(), session.SessionId,
                session.ConferenceId, request.UserId, request.Email.Trim(), session.PriceMinor, session.Currency,
                _options.PlatformCommissionPercent, now)
            {
                Shares = session.Shares.ToList(),
            };
            await _orders.Create(order);

            try
            {
                var checkout = await _provider.InitializeCheckout(new CheckoutRequest(order.Reference, order.Email,
                    order.AmountMinor, order.Currency, request.CallbackUrl, session.Shares[0].SubaccountCode,
                    PriceSplit.PlatformAmount(order.AmountMinor, order.CommissionPercent),
                    new System.Collections.Generic.Dictionary<string, string>
                        {{"sessionId", session.SessionId}, {"orderId", order.OrderId}}), cancellationToken);
                return new CheckoutResult(checkout.AuthorizationUrl, order.Reference, false);
            }
            catch (PaymentProviderException e)
            {
                _logger.LogWarning(e, "The checkout of order {orderId} could not be started", order.OrderId);
                await _orders.MarkFailed(order.Reference, "The checkout could not be started.");
                return PaymentsError.ProviderUnavailable;
            }
        }
    }

    public class SettleOrderUseCase : IRequestHandler<SettleOrderRequest, SuccessOrError<TicketOrder>>
    {
        private readonly IPaymentProvider _provider;
        private readonly IOrderRepo _orders;
        private readonly IEntitlementRepo _entitlements;
        private readonly IPassRepo _passes;
        private readonly IPassSubscriptionRepo _subscriptions;
        private readonly IVideoEntitlementRepo _videoEntitlements;
        private readonly PaymentsOptions _options;
        private readonly ISessionRepo _sessions;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<SettleOrderUseCase> _logger;

        public SettleOrderUseCase(IPaymentProvider provider, IOrderRepo orders, IEntitlementRepo entitlements,
            IPassRepo passes, IPassSubscriptionRepo subscriptions, IVideoEntitlementRepo videoEntitlements,
            IOptions<PaymentsOptions> options, ISessionRepo sessions, TimeProvider timeProvider,
            ILogger<SettleOrderUseCase> logger)
        {
            _videoEntitlements = videoEntitlements;
            _passes = passes;
            _subscriptions = subscriptions;
            _options = options.Value;
            _provider = provider;
            _orders = orders;
            _entitlements = entitlements;
            _sessions = sessions;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<SuccessOrError<TicketOrder>> Handle(SettleOrderRequest request,
            CancellationToken cancellationToken)
        {
            var order = await _orders.FindByReference(request.Reference);
            if (order == null) return PaymentsError.OrderNotFound;

            // already settled: make sure the ticket exists (a crash between the two steps must not lose it)
            if (order.Status == OrderStatus.Paid)
            {
                await GrantTicket(order);
                return order;
            }

            if (order.Status is OrderStatus.Failed or OrderStatus.Refunded) return order;

            VerifiedTransaction verified;
            try
            {
                verified = await _provider.VerifyTransaction(order.Reference, cancellationToken);
            }
            catch (TransactionNotFoundException)
            {
                // the person has not paid (yet): the order stays as it is
                return order;
            }
            catch (PaymentProviderException e)
            {
                _logger.LogWarning(e, "Order {reference} could not be verified", order.Reference);
                return PaymentsError.ProviderUnavailable;
            }

            if (verified.Failed)
            {
                await _orders.MarkFailed(order.Reference, "The payment failed.");
                order.Status = OrderStatus.Failed;
                return order;
            }

            if (!verified.Paid) return order;

            // what was paid must be what was ordered: a notification or a browser must never decide the amount
            if (verified.AmountMinor != order.AmountMinor ||
                !string.Equals(verified.Currency, order.Currency, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(verified.Reference, order.Reference, StringComparison.Ordinal))
            {
                _logger.LogError(
                    "Order {reference} was paid with another amount or currency ({paidAmount} {paidCurrency} instead of {amount} {currency})",
                    order.Reference, verified.AmountMinor, verified.Currency, order.AmountMinor, order.Currency);
                await _orders.MarkFailed(order.Reference, "The amount that was paid does not match the order.");
                order.Status = OrderStatus.Failed;
                return order;
            }

            var allocations = PriceSplit.Allocate(order.AmountMinor, order.CommissionPercent, order.Shares.ToList())
                .ToList();
            await _orders.TryMarkPaid(order.Reference, verified.PaidAt ?? DateTimeOffset.UtcNow,
                verified.ProviderTransactionId, verified.Channel, allocations);

            // whoever settled it first, the ticket is granted once
            var settled = await _orders.FindByReference(order.Reference) ?? order;

            // paid while the session was being called off: no ticket, the money goes back
            var session = settled.Kind == OrderKind.Ticket ? await _sessions.FindById(settled.SessionId) : null;
            if (session?.Status == SessionStatus.Cancelled)
            {
                await OrderRefunds.Refund(_provider, _orders, _timeProvider, _logger, settled, cancellationToken);
                return await _orders.FindByReference(order.Reference) ?? settled;
            }

            await GrantTicket(settled);

            // a pass that was paid with a card that may be charged again, and that the person wants renewed
            if (settled.Kind == OrderKind.Pass && settled.AutoRenew && _options.AutoRenewEnabled &&
                verified.AuthorizationCode != null)
                await StartSubscription(settled, verified.AuthorizationCode);

            return settled;
        }

        private async Task StartSubscription(TicketOrder order, string authorizationCode)
        {
            var now = _timeProvider.GetUtcNow();
            // charged a day before the pass ends: the new pass is added to the end of the running one
            var validUntil = await _passes.ActiveUntil(order.BuyerUserId, order.PassCreatorId!, now) ??
                             now.AddDays(PassRules.Days);
            await _subscriptions.StartOrReplace(new PassSubscription(Guid.NewGuid().ToString("N"), order.BuyerUserId,
                order.PassCreatorId!, order.Email, authorizationCode, order.AmountMinor, order.Currency, now,
                validUntil.AddDays(-1)));
        }

        private async Task GrantTicket(TicketOrder order)
        {
            if (order.Kind == OrderKind.Video)
            {
                await _videoEntitlements.GrantOnce(new Strive.Core.Services.Videos.VideoEntitlement(
                    Guid.NewGuid().ToString("N"), order.BuyerUserId, order.VideoId!, order.OrderId,
                    _timeProvider.GetUtcNow()));
                return;
            }

            if (order.Kind == OrderKind.Pass)
            {
                await _passes.GrantOnce(order.BuyerUserId, order.PassCreatorId!, order.OrderId,
                    _timeProvider.GetUtcNow(), PassRules.Days);
                return;
            }

            await _entitlements.GrantOnce(new Entitlement(Guid.NewGuid().ToString("N"), order.BuyerUserId,
                order.SessionId, order.ConferenceId, order.OrderId, DateTimeOffset.UtcNow));
        }
    }

    public class CheckSessionAccessUseCase : IRequestHandler<CheckSessionAccessRequest, SuccessOrError<Unit>>
    {
        private readonly IMediator _mediator;
        private readonly ISessionRepo _sessions;
        private readonly IEntitlementRepo _entitlements;
        private readonly IPassRepo _passes;
        private readonly TimeProvider _timeProvider;
        private readonly IOptions<PaymentsOptions> _options;

        public CheckSessionAccessUseCase(IMediator mediator, ISessionRepo sessions, IEntitlementRepo entitlements,
            IPassRepo passes, TimeProvider timeProvider, IOptions<PaymentsOptions> options)
        {
            _passes = passes;
            _timeProvider = timeProvider;
            _mediator = mediator;
            _sessions = sessions;
            _entitlements = entitlements;
            _options = options;
        }

        public async Task<SuccessOrError<Unit>> Handle(CheckSessionAccessRequest request,
            CancellationToken cancellationToken)
        {
            var (conferenceId, participantId) = request.Participant;

            var session = await _sessions.FindByConferenceId(conferenceId);
            // an ordinary conference: nothing to check
            if (session == null) return Unit.Value;

            if (session.CreatorUserId == participantId) return Unit.Value;

            var conference = await _mediator.Send(new FindConferenceByIdRequest(conferenceId), cancellationToken);
            if (conference.Configuration.Moderators.Contains(participantId)) return Unit.Value;

            if (session.Status == SessionStatus.Cancelled) return PaymentsError.SessionCancelled;

            if (await _entitlements.Holds(participantId, conferenceId)) return Unit.Value;

            // a running pass of the host is as good as a ticket
            return await _passes.ActiveUntil(participantId, session.CreatorId, _timeProvider.GetUtcNow()) != null
                ? Unit.Value
                : PaymentsError.TicketRequired;
        }
    }

    internal static class OrderRefunds
    {
        /// <summary>Gives a paid order back. A failure is written on the order and logged, never thrown.</summary>
        public static async Task Refund(IPaymentProvider provider, IOrderRepo orders, TimeProvider timeProvider,
            ILogger logger, TicketOrder order, CancellationToken cancellationToken)
        {
            try
            {
                var refund = await provider.RefundTransaction(order.Reference, order.AmountMinor, cancellationToken);
                await orders.TryMarkRefunded(order.Reference, timeProvider.GetUtcNow(), refund.RefundId);
            }
            catch (PaymentProviderException e)
            {
                logger.LogWarning(e, "Refund of order {reference} did not work", order.Reference);
                await orders.RecordRefundFailure(order.Reference, e.Message);
            }
        }
    }

    public class CancelSessionUseCase : IRequestHandler<CancelSessionRequest, SuccessOrError<Unit>>
    {
        private readonly ISessionRepo _sessions;
        private readonly IOrderRepo _orders;
        private readonly IPaymentProvider _provider;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<CancelSessionUseCase> _logger;

        public CancelSessionUseCase(ISessionRepo sessions, IOrderRepo orders, IPaymentProvider provider,
            TimeProvider timeProvider, ILogger<CancelSessionUseCase> logger)
        {
            _sessions = sessions;
            _orders = orders;
            _provider = provider;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        /// <summary>
        ///     Calls the session off and gives every ticket money back. Doing it again on a session that is already called
        ///     off tries the refunds that did not work before; a refund that fails never undoes the cancellation.
        /// </summary>
        public async Task<SuccessOrError<Unit>> Handle(CancelSessionRequest request, CancellationToken cancellationToken)
        {
            var session = await _sessions.FindById(request.SessionId);
            if (session == null || session.CreatorUserId != request.UserId) return PaymentsError.SessionNotFound;

            if (session.Status != SessionStatus.Cancelled)
            {
                session.Status = SessionStatus.Cancelled;
                await _sessions.Update(session);
            }

            foreach (var order in await _orders.FindPaidOfSession(session.SessionId))
            {
                if (order.Status != OrderStatus.Paid) continue;

                await OrderRefunds.Refund(_provider, _orders, _timeProvider, _logger, order, cancellationToken);
            }

            return Unit.Value;
        }
    }

    public class StartPassCheckoutUseCase : IRequestHandler<StartPassCheckoutRequest, SuccessOrError<CheckoutResult>>
    {
        private readonly IPaymentProvider _provider;
        private readonly ICreatorRepo _creators;
        private readonly IOrderRepo _orders;
        private readonly PaymentsOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<StartPassCheckoutUseCase> _logger;

        public StartPassCheckoutUseCase(IPaymentProvider provider, ICreatorRepo creators, IOrderRepo orders,
            IOptions<PaymentsOptions> options, TimeProvider timeProvider, ILogger<StartPassCheckoutUseCase> logger)
        {
            _provider = provider;
            _creators = creators;
            _orders = orders;
            _options = options.Value;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<SuccessOrError<CheckoutResult>> Handle(StartPassCheckoutRequest request,
            CancellationToken cancellationToken)
        {
            if (!_options.Enabled) return PaymentsError.NotEnabled;
            if (!SessionRules.IsValidEmail(request.Email)) return PaymentsError.InvalidEmail;

            var creator = await _creators.FindById(request.CreatorId);
            if (creator == null || !creator.CanSell) return PaymentsError.CreatorNotFound;
            if (creator.PassPriceMinor is not { } price) return PaymentsError.PassNotOffered;
            if (creator.UserId == request.UserId) return PaymentsError.OwnPass;
            if (request.AutoRenew && !_options.AutoRenewEnabled) return PaymentsError.AutoRenewUnavailable;

            // buying again while a pass runs is fine: the time is added to it
            var order = new TicketOrder(Guid.NewGuid().ToString("N"), SessionRules.NewReference(), "", "",
                request.UserId, request.Email.Trim(), price, creator.Currency, _options.PlatformCommissionPercent,
                _timeProvider.GetUtcNow())
            {
                Kind = OrderKind.Pass,
                PassCreatorId = creator.CreatorId,
                AutoRenew = request.AutoRenew,
                Shares = new System.Collections.Generic.List<RevenueShare>
                {
                    new(creator.CreatorId, creator.ProviderSubaccountCode!, 100),
                },
            };
            await _orders.Create(order);

            try
            {
                var checkout = await _provider.InitializeCheckout(new CheckoutRequest(order.Reference, order.Email,
                    order.AmountMinor, order.Currency, request.CallbackUrl, creator.ProviderSubaccountCode!,
                    PriceSplit.PlatformAmount(order.AmountMinor, order.CommissionPercent),
                    new System.Collections.Generic.Dictionary<string, string>
                        {{"kind", "pass"}, {"creatorId", creator.CreatorId}, {"orderId", order.OrderId}}),
                    cancellationToken);
                return new CheckoutResult(checkout.AuthorizationUrl, order.Reference, false);
            }
            catch (PaymentProviderException e)
            {
                _logger.LogWarning(e, "The checkout of pass order {orderId} could not be started", order.OrderId);
                await _orders.MarkFailed(order.Reference, "The checkout could not be started.");
                return PaymentsError.ProviderUnavailable;
            }
        }
    }

    public class SetPassPriceUseCase : IRequestHandler<SetPassPriceRequest, SuccessOrError<CreatorProfile>>
    {
        private readonly ICreatorRepo _creators;
        private readonly PaymentsOptions _options;
        private readonly TimeProvider _timeProvider;

        public SetPassPriceUseCase(ICreatorRepo creators, IOptions<PaymentsOptions> options, TimeProvider timeProvider)
        {
            _creators = creators;
            _options = options.Value;
            _timeProvider = timeProvider;
        }

        public async Task<SuccessOrError<CreatorProfile>> Handle(SetPassPriceRequest request,
            CancellationToken cancellationToken)
        {
            if (!_options.Enabled) return PaymentsError.NotEnabled;

            var creator = await _creators.FindByUserId(request.UserId);
            if (creator == null) return PaymentsError.CreatorNotFound;
            if (!creator.CanSell) return PaymentsError.CreatorCannotSell;

            if (request.PriceMinor is { } price and (< SessionRules.MinPriceMinor or > SessionRules.MaxPriceMinor))
                return PaymentsError.InvalidSession(
                    $"The price must be between {SessionRules.MinPriceMinor / 100} and {SessionRules.MaxPriceMinor / 100} {creator.Currency}.");

            creator.PassPriceMinor = request.PriceMinor;
            creator.UpdatedAt = _timeProvider.GetUtcNow();
            await _creators.Save(creator);
            return creator;
        }
    }

    public class SetSlugUseCase : IRequestHandler<SetSlugRequest, SuccessOrError<CreatorProfile>>
    {
        private readonly ICreatorRepo _creators;
        private readonly TimeProvider _timeProvider;

        public SetSlugUseCase(ICreatorRepo creators, TimeProvider timeProvider)
        {
            _creators = creators;
            _timeProvider = timeProvider;
        }

        public async Task<SuccessOrError<CreatorProfile>> Handle(SetSlugRequest request,
            CancellationToken cancellationToken)
        {
            var creator = await _creators.FindByUserId(request.UserId);
            if (creator == null) return PaymentsError.CreatorNotFound;

            var slug = string.IsNullOrWhiteSpace(request.Slug) ? null : SlugRules.Normalize(request.Slug);
            if (slug != null)
            {
                var problem = SlugRules.Problem(slug);
                if (problem != null) return PaymentsError.InvalidSlug(problem);

                var owner = await _creators.FindBySlug(slug);
                if (owner != null && owner.CreatorId != creator.CreatorId) return PaymentsError.SlugTaken;
            }

            creator.Slug = slug;
            creator.UpdatedAt = _timeProvider.GetUtcNow();
            try
            {
                await _creators.Save(creator);
            }
            catch (SlugTakenException)
            {
                return PaymentsError.SlugTaken;
            }

            return creator;
        }
    }

    public class CancelPassSubscriptionUseCase : IRequestHandler<CancelPassSubscriptionRequest, SuccessOrError<Unit>>
    {
        private readonly IPassSubscriptionRepo _subscriptions;
        private readonly TimeProvider _timeProvider;

        public CancelPassSubscriptionUseCase(IPassSubscriptionRepo subscriptions, TimeProvider timeProvider)
        {
            _subscriptions = subscriptions;
            _timeProvider = timeProvider;
        }

        public async Task<SuccessOrError<Unit>> Handle(CancelPassSubscriptionRequest request,
            CancellationToken cancellationToken)
        {
            var subscription = await _subscriptions.FindById(request.SubscriptionId);
            // somebody else's subscription does not exist for you
            if (subscription == null || subscription.UserId != request.UserId)
                return PaymentsError.SubscriptionNotFound;

            if (subscription.Status == SubscriptionStatus.Active)
            {
                subscription.Status = SubscriptionStatus.Cancelled;
                subscription.EndedAt = _timeProvider.GetUtcNow();
                subscription.PendingReference = null;
                await _subscriptions.Update(subscription);
            }

            return Unit.Value;
        }
    }

    public class RenewPassSubscriptionsUseCase
        : IRequestHandler<RenewPassSubscriptionsRequest, SuccessOrError<RenewalSummary>>
    {
        private static readonly TimeSpan Lease = TimeSpan.FromHours(1);

        private readonly IPassSubscriptionRepo _subscriptions;
        private readonly ICreatorRepo _creators;
        private readonly IOrderRepo _orders;
        private readonly IPassRepo _passes;
        private readonly IPaymentProvider _provider;
        private readonly IMediator _mediator;
        private readonly IEmailSender _email;
        private readonly PaymentsOptions _options;
        private readonly RemindersOptions _reminders;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<RenewPassSubscriptionsUseCase> _logger;

        public RenewPassSubscriptionsUseCase(IPassSubscriptionRepo subscriptions, ICreatorRepo creators,
            IOrderRepo orders, IPassRepo passes, IPaymentProvider provider, IMediator mediator, IEmailSender email,
            IOptions<PaymentsOptions> options, IOptions<RemindersOptions> reminders, TimeProvider timeProvider,
            ILogger<RenewPassSubscriptionsUseCase> logger)
        {
            _subscriptions = subscriptions;
            _creators = creators;
            _orders = orders;
            _passes = passes;
            _provider = provider;
            _mediator = mediator;
            _email = email;
            _options = options.Value;
            _reminders = reminders.Value;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<SuccessOrError<RenewalSummary>> Handle(RenewPassSubscriptionsRequest request,
            CancellationToken cancellationToken)
        {
            if (!_options.Enabled || !_options.AutoRenewEnabled) return new RenewalSummary(0, 0, 0, 0);

            int renewed = 0, failed = 0, ended = 0;
            var now = _timeProvider.GetUtcNow();
            foreach (var due in await _subscriptions.FindDue(now, 100))
            {
                // only one round (and one server) charges a renewal
                if (!await _subscriptions.TryStartCharge(due.SubscriptionId, now, now + Lease)) continue;

                var subscription = await _subscriptions.FindById(due.SubscriptionId);
                if (subscription is not {Status: SubscriptionStatus.Active}) continue;

                switch (await Renew(subscription, now, cancellationToken))
                {
                    case RenewalOutcome.Renewed: renewed++; break;
                    case RenewalOutcome.Failed: failed++; break;
                    case RenewalOutcome.Ended: ended++; break;
                }
            }

            return new RenewalSummary(renewed, failed, ended, await Notify(now, cancellationToken));
        }

        private enum RenewalOutcome
        {
            Renewed,
            Failed,
            Ended,
        }

        private async Task<RenewalOutcome> Renew(PassSubscription subscription, DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            var creator = await _creators.FindById(subscription.CreatorId);
            TicketOrder? order = null;

            if (subscription.PendingReference != null)
            {
                // a charge was started earlier and nobody knows how it ended: look at that one, never charge twice
                var settled = await _mediator.Send(new SettleOrderRequest(subscription.PendingReference),
                    cancellationToken);
                order = settled.Success ? settled.Response : null;
            }
            else
            {
                if (creator is not {CanSell: true, PassPriceMinor: { } price})
                    return await End(subscription, creator, now, "the creator no longer sells this pass");

                // the person never pays more than they agreed to, and gets a lower price if the creator lowered it
                var amount = Math.Min(subscription.AmountMinor, price);
                order = new TicketOrder(Guid.NewGuid().ToString("N"), SessionRules.NewReference(), "", "",
                    subscription.UserId, subscription.Email, amount, subscription.Currency,
                    _options.PlatformCommissionPercent, now)
                {
                    Kind = OrderKind.Pass,
                    PassCreatorId = subscription.CreatorId,
                    IsRenewal = true,
                    Shares = new System.Collections.Generic.List<RevenueShare>
                    {
                        new(creator.CreatorId, creator.ProviderSubaccountCode!, 100),
                    },
                };
                await _orders.Create(order);
                subscription.PendingReference = order.Reference;
                await _subscriptions.Update(subscription);

                try
                {
                    await _provider.ChargeAuthorization(new ChargeAuthorizationRequest(order.Reference,
                        order.Email, subscription.AuthorizationCode, order.AmountMinor, order.Currency,
                        creator.ProviderSubaccountCode!,
                        PriceSplit.PlatformAmount(order.AmountMinor, order.CommissionPercent),
                        new System.Collections.Generic.Dictionary<string, string>
                        {
                            {"kind", "pass-renewal"}, {"creatorId", creator.CreatorId},
                            {"subscriptionId", subscription.SubscriptionId},
                        }), cancellationToken);
                }
                catch (PaymentProviderException e)
                {
                    _logger.LogWarning(e, "The renewal of subscription {id} was refused", subscription.SubscriptionId);
                    await _orders.MarkFailed(order.Reference, "The renewal was refused.");
                    return await Fail(subscription, creator, now, false);
                }

                var settled = await _mediator.Send(new SettleOrderRequest(order.Reference), cancellationToken);
                order = settled.Success ? settled.Response : null;
            }

            switch (order?.Status)
            {
                case OrderStatus.Paid:
                    var validUntil = await _passes.ActiveUntil(subscription.UserId, subscription.CreatorId, now) ??
                                     now.AddDays(PassRules.Days);
                    subscription.PendingReference = null;
                    subscription.FailedAttempts = 0;
                    subscription.NextChargeAt = validUntil.AddDays(-1);
                    await _subscriptions.Update(subscription);
                    return RenewalOutcome.Renewed;
                case OrderStatus.Failed:
                    subscription.PendingReference = null;
                    return await Fail(subscription, creator, now, false);
                default:
                    // not known yet (or the provider could not be asked): look again in a while, never charge again
                    return await Fail(subscription, creator, now, true);
            }
        }

        private async Task<RenewalOutcome> Fail(PassSubscription subscription, CreatorProfile? creator,
            DateTimeOffset now, bool keepPending)
        {
            subscription.FailedAttempts++;
            if (!keepPending) subscription.PendingReference = null;

            if (subscription.FailedAttempts >= _options.RenewMaxAttempts)
                return await End(subscription, creator, now, "the payment did not work");

            subscription.NextChargeAt = keepPending ? now + Lease : now.AddHours(_options.RenewRetryHours);
            await _subscriptions.Update(subscription);
            await Mail(subscription, creator,
                $"Your pass renewal did not go through",
                $"We could not renew your pass for {creator?.DisplayName ?? "this creator"}. We will try again on " +
                $"{ReminderMail.FormatTime(subscription.NextChargeAt, creator?.Country ?? "NG")}. " +
                "Check that your card has money on it and has not expired. You can stop the renewal on the page My tickets.");
            return RenewalOutcome.Failed;
        }

        private async Task<RenewalOutcome> End(PassSubscription subscription, CreatorProfile? creator,
            DateTimeOffset now, string reason)
        {
            subscription.Status = SubscriptionStatus.Ended;
            subscription.EndedAt = now;
            subscription.PendingReference = null;
            await _subscriptions.Update(subscription);
            _logger.LogInformation("Subscription {id} ended: {reason}", subscription.SubscriptionId, reason);
            await Mail(subscription, creator, "Your pass is not renewed any more",
                $"Your pass for {creator?.DisplayName ?? "this creator"} is not renewed any more ({reason}). " +
                "What you paid for runs until its end. You can buy a new pass on the page of the creator.");
            return RenewalOutcome.Ended;
        }

        /// <summary>Tells the people whose renewal comes within a few days, once, so that nobody is charged by surprise</summary>
        private async Task<int> Notify(DateTimeOffset now, CancellationToken cancellationToken)
        {
            if (!_email.IsConfigured) return 0;

            var sent = 0;
            var until = now.AddDays(_options.RenewNoticeDays);
            foreach (var subscription in await _subscriptions.FindDueForNotice(until, 100))
            {
                // only a renewal that is still to come and that is not being retried
                if (subscription.NextChargeAt <= now || subscription.FailedAttempts > 0 ||
                    subscription.PendingReference != null)
                    continue;

                var creator = await _creators.FindById(subscription.CreatorId);
                var amount = Math.Min(subscription.AmountMinor, creator?.PassPriceMinor ?? subscription.AmountMinor);
                var link = $"{_reminders.LinkBaseUrl.TrimEnd('/')}/tickets";
                var delivered = await Mail(subscription, creator, "Your pass renews soon",
                    $"Your pass for {creator?.DisplayName ?? "this creator"} renews on " +
                    $"{ReminderMail.FormatTime(subscription.NextChargeAt, creator?.Country ?? "NG")}: " +
                    $"{amount / 100m:N0} {subscription.Currency} will be charged to the card you used. " +
                    $"To stop it, open {link} and choose Stop renewing.");

                // told or not, ask again only when the time of the renewal changes
                subscription.NoticeSentFor = subscription.NextChargeAt;
                await _subscriptions.Update(subscription);
                if (delivered) sent++;
            }

            return sent;
        }

        private async Task<bool> Mail(PassSubscription subscription, CreatorProfile? creator, string subject,
            string text)
        {
            if (!_email.IsConfigured) return false;

            try
            {
                await _email.Send(subscription.Email, subject,
                    $"<p>{System.Net.WebUtility.HtmlEncode(text)}</p>", text);
                return true;
            }
            catch (EmailException e)
            {
                _logger.LogWarning(e, "A mail about subscription {id} could not be sent", subscription.SubscriptionId);
                return false;
            }
        }
    }

    public class SetCreatorPhotoUseCase : IRequestHandler<SetCreatorPhotoRequest, SuccessOrError<CreatorProfile>>
    {
        private readonly ICreatorRepo _creators;
        private readonly IPhotoStorage _photos;
        private readonly TimeProvider _timeProvider;

        public SetCreatorPhotoUseCase(ICreatorRepo creators, IPhotoStorage photos, TimeProvider timeProvider)
        {
            _creators = creators;
            _photos = photos;
            _timeProvider = timeProvider;
        }

        public async Task<SuccessOrError<CreatorProfile>> Handle(SetCreatorPhotoRequest request,
            CancellationToken cancellationToken)
        {
            var creator = await _creators.FindByUserId(request.UserId);
            if (creator == null) return PaymentsError.CreatorNotFound;

            var now = _timeProvider.GetUtcNow();
            if (request.Data == null)
            {
                await _photos.Remove(creator.CreatorId, creator.PhotoPublicId, cancellationToken);
                creator.PhotoVersion = null;
                creator.PhotoPublicId = null;
            }
            else
            {
                if (request.Data.Length == 0) return PaymentsError.InvalidPhoto("Choose a picture.");
                if (request.Data.Length > PhotoRules.MaxBytes)
                    return PaymentsError.InvalidPhoto($"The picture is too big ({PhotoRules.MaxBytes / 1024} KB at most).");

                var type = PhotoRules.Sniff(request.Data);
                if (type == null) return PaymentsError.InvalidPhoto("Use a jpeg, png or webp picture.");

                StoredPhoto stored;
                try
                {
                    stored = await _photos.Save(creator.CreatorId, type, request.Data, cancellationToken);
                }
                catch (PhotoStorageException)
                {
                    // the profile keeps the photo it has
                    return PaymentsError.InvalidPhoto("The picture could not be saved. Try again in a moment.");
                }

                creator.PhotoVersion = stored.Version;
                creator.PhotoPublicId = stored.PublicId;
            }

            creator.UpdatedAt = now;
            await _creators.Save(creator);
            return creator;
        }
    }
}
