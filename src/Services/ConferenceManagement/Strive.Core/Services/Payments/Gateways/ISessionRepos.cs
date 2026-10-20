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

        /// <summary>The orders of a session that were paid (and maybe given back since)</summary>
        Task<IReadOnlyList<TicketOrder>> FindPaidOfSession(string sessionId);

        /// <summary>Marks a paid order as refunded. Only a paid order changes.</summary>
        Task<bool> TryMarkRefunded(string reference, System.DateTimeOffset refundedAt, string? refundId);

        Task RecordRefundFailure(string reference, string reason);
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
