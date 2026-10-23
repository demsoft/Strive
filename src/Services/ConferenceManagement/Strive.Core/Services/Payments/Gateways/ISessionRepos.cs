using System.Collections.Generic;
using System.Threading.Tasks;
using Strive.Core.Services.Payments.Sessions;

namespace Strive.Core.Services.Payments.Gateways
{
    public interface ISessionRepo
    {
        Task Create(PaidSession session);

        Task<PaidSession?> FindById(string sessionId);

        Task<PaidSession?> FindByConferenceId(string conferenceId);

        Task<IReadOnlyList<PaidSession>> FindOfCreator(string creatorId, int limit);

        /// <summary>Published sessions that are not over: no start time, or one that is not before <paramref name="notBefore" /></summary>
        Task<IReadOnlyList<PaidSession>> FindForSale(System.DateTimeOffset notBefore, int limit);

        /// <summary>The same as <see cref="FindForSale" />, for one creator</summary>
        Task<IReadOnlyList<PaidSession>> FindForSaleOfCreator(string creatorId, System.DateTimeOffset notBefore,
            int limit);

        /// <summary>Published sessions that start between the two times and have not been reminded yet</summary>
        Task<IReadOnlyList<PaidSession>> FindDueForReminder(System.DateTimeOffset from, System.DateTimeOffset until);

        /// <summary>Marks the reminder as taken. Only the first call for a session returns true.</summary>
        Task<bool> TryClaimReminder(string sessionId, System.DateTimeOffset at);

        /// <summary>Gives the reminder back (nobody could be reached), so that it is tried again</summary>
        Task ReleaseReminder(string sessionId);

        Task Update(PaidSession session);
    }

    public interface IOrderRepo
    {
        Task Create(TicketOrder order);

        Task<TicketOrder?> FindByReference(string reference);

        /// <summary>
        ///     Marks a pending order as paid. Only the first call does it, so that two notifications at the same time
        ///     cannot both settle the order.
        /// </summary>
        /// <returns>True if this call settled the order</returns>
        Task<bool> TryMarkPaid(string reference, System.DateTimeOffset paidAt, string? providerTransactionId,
            string? channel, IList<OrderAllocation> allocations);

        Task MarkFailed(string reference, string reason);

        Task<TicketOrder?> FindById(string orderId);

        /// <summary>The latest orders of every status, newest first (for the administrators)</summary>
        Task<IReadOnlyList<TicketOrder>> FindRecent(int limit);

        /// <summary>Paid and refunded orders made since the time, newest first (for the administrators)</summary>
        Task<IReadOnlyList<TicketOrder>> FindPaidSince(System.DateTimeOffset since, int limit);

        /// <summary>Paid orders whose refund did not work: the money is still with the platform, the person wants it back</summary>
        Task<IReadOnlyList<TicketOrder>> FindWithRefundFailure(int limit);

        /// <summary>The paid (and refunded) orders that include a share of the creator, newest first</summary>
        Task<IReadOnlyList<TicketOrder>> FindPaidOfCreator(string creatorId, int limit);

        /// <summary>The orders of a session that were paid (and maybe given back since)</summary>
        Task<IReadOnlyList<TicketOrder>> FindPaidOfSession(string sessionId);

        /// <summary>Marks a paid order as refunded. Only a paid order changes.</summary>
        Task<bool> TryMarkRefunded(string reference, System.DateTimeOffset refundedAt, string? refundId);

        Task RecordRefundFailure(string reference, string reason);
    }

    public interface IPassSubscriptionRepo
    {
        /// <summary>
        ///     Stores the subscription of a person for a creator. A person has one that is active per creator: if there is
        ///     one, it takes the new card, price and time and stays the same subscription.
        /// </summary>
        Task<PassSubscription> StartOrReplace(PassSubscription subscription);

        Task<PassSubscription?> FindById(string subscriptionId);

        /// <summary>The active subscriptions of a person</summary>
        Task<IReadOnlyList<PassSubscription>> FindActiveOfUser(string userId);

        Task<int> CountActive();

        /// <summary>Subscriptions that need a look: renewals that fail, and ones that ended since the time</summary>
        Task<IReadOnlyList<PassSubscription>> FindWithProblems(System.DateTimeOffset endedSince, int limit);

        /// <summary>Active subscriptions whose renewal is due</summary>
        Task<IReadOnlyList<PassSubscription>> FindDue(System.DateTimeOffset now, int limit);

        /// <summary>Active subscriptions whose renewal comes within the time and that were not told about it yet</summary>
        Task<IReadOnlyList<PassSubscription>> FindDueForNotice(System.DateTimeOffset until, int limit);

        /// <summary>
        ///     Takes the renewal: only one caller gets true for a due subscription, the next charge time moves to
        ///     <paramref name="leaseUntil" /> so that a second round (or server) does not charge again.
        /// </summary>
        Task<bool> TryStartCharge(string subscriptionId, System.DateTimeOffset now, System.DateTimeOffset leaseUntil);

        Task Update(PassSubscription subscription);
    }

    public interface IPassRepo
    {
        /// <summary>
        ///     Grants the pass once per order: a second call with the same order changes nothing. The pass runs from now,
        ///     or from the end of the pass the person has already (buying early does not waste time).
        /// </summary>
        /// <returns>The pass of the order</returns>
        Task<PassEntitlement> GrantOnce(string userId, string creatorId, string orderId, System.DateTimeOffset now,
            int days);

        /// <summary>When the pass of the person for this creator ends, if it is running</summary>
        Task<System.DateTimeOffset?> ActiveUntil(string userId, string creatorId, System.DateTimeOffset now);

        /// <summary>The passes of a person that are running</summary>
        Task<IReadOnlyList<PassEntitlement>> FindActiveOfUser(string userId, System.DateTimeOffset now);
    }

    public interface IEntitlementRepo
    {
        /// <summary>Grants the right once per order: a second call with the same order changes nothing</summary>
        /// <returns>True if the right was new</returns>
        Task<bool> GrantOnce(Entitlement entitlement);

        Task<bool> Holds(string userId, string conferenceId);

        Task<IReadOnlyList<Entitlement>> FindOfUser(string userId, int limit);
    }
}
