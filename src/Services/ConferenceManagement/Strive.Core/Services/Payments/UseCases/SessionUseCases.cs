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

namespace Strive.Core.Services.Payments.UseCases
{
    public static class SessionRules
    {
        /// <summary>₦100: below that the fees of the payment would be most of it</summary>
        public const long MinPriceMinor = 10_000;

        /// <summary>₦1,000,000</summary>
        public const long MaxPriceMinor = 100_000_000;

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
        private readonly ISessionRepo _sessions;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<SettleOrderUseCase> _logger;

        public SettleOrderUseCase(IPaymentProvider provider, IOrderRepo orders, IEntitlementRepo entitlements,
            IPassRepo passes, ISessionRepo sessions, TimeProvider timeProvider, ILogger<SettleOrderUseCase> logger)
        {
            _passes = passes;
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
            return settled;
        }

        private async Task GrantTicket(TicketOrder order)
        {
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

            // buying again while a pass runs is fine: the time is added to it
            var order = new TicketOrder(Guid.NewGuid().ToString("N"), SessionRules.NewReference(), "", "",
                request.UserId, request.Email.Trim(), price, creator.Currency, _options.PlatformCommissionPercent,
                _timeProvider.GetUtcNow())
            {
                Kind = OrderKind.Pass,
                PassCreatorId = creator.CreatorId,
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
}
