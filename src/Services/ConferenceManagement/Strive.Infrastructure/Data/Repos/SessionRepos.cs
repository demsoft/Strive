using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Strive.Core.Services.Payments.Gateways;
using Strive.Core.Services.Payments.Sessions;

namespace Strive.Infrastructure.Data.Repos
{
    public class PaidSessionRepo : MongoRepo<PaidSession>, IMongoIndexBuilder, ISessionRepo
    {
        static PaidSessionRepo()
        {
            BsonClassMap.RegisterClassMap<PaidSession>(config =>
            {
                config.AutoMap();
                config.MapIdMember(x => x.SessionId);
                config.MapMember(x => x.Status).SetSerializer(new EnumSerializer<SessionStatus>(BsonType.String));
                config.MapMember(x => x.CreatedAt).SetSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
                config.MapMember(x => x.StartsAt).SetSerializer(
                    new NullableSerializer<DateTimeOffset>(new DateTimeOffsetSerializer(BsonType.DateTime)));
                config.MapMember(x => x.ReminderSentAt).SetSerializer(
                    new NullableSerializer<DateTimeOffset>(new DateTimeOffsetSerializer(BsonType.DateTime)));
                config.SetIgnoreExtraElements(true);
            });
            BsonClassMap.RegisterClassMap<RevenueShare>(config =>
            {
                config.AutoMap();
                config.SetIgnoreExtraElements(true);
            });
        }

        public PaidSessionRepo(IOptions<MongoDbOptions> options) : base(options)
        {
        }

        public async Task CreateIndexes()
        {
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<PaidSession>(
                Builders<PaidSession>.IndexKeys.Ascending(x => x.ConferenceId), new CreateIndexOptions {Unique = true}));
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<PaidSession>(
                Builders<PaidSession>.IndexKeys.Ascending(x => x.CreatorId).Descending(x => x.CreatedAt)));
        }

        public Task Create(PaidSession session) => Collection.InsertOneAsync(session);

        public Task<PaidSession?> FindById(string sessionId) =>
            Collection.Find(x => x.SessionId == sessionId).FirstOrDefaultAsync()!;

        public Task<PaidSession?> FindByConferenceId(string conferenceId) =>
            Collection.Find(x => x.ConferenceId == conferenceId).FirstOrDefaultAsync()!;

        public async Task<IReadOnlyList<PaidSession>> FindOfCreator(string creatorId, int limit) =>
            await Collection.Find(x => x.CreatorId == creatorId).SortByDescending(x => x.CreatedAt).Limit(limit)
                .ToListAsync();

        public async Task<IReadOnlyList<PaidSession>> FindForSale(DateTimeOffset notBefore, int limit) =>
            await Collection.Find(x => x.Status == SessionStatus.Published &&
                                       (x.StartsAt == null || x.StartsAt >= notBefore))
                .SortByDescending(x => x.CreatedAt).Limit(limit).ToListAsync();

        public async Task<IReadOnlyList<PaidSession>> FindForSaleOfCreator(string creatorId, DateTimeOffset notBefore,
            int limit) =>
            await Collection.Find(x => x.CreatorId == creatorId && x.Status == SessionStatus.Published &&
                                       (x.StartsAt == null || x.StartsAt >= notBefore))
                .SortByDescending(x => x.CreatedAt).Limit(limit).ToListAsync();

        public async Task<IReadOnlyList<PaidSession>> FindDueForReminder(DateTimeOffset from, DateTimeOffset until) =>
            await Collection.Find(x => x.Status == SessionStatus.Published && x.ReminderSentAt == null &&
                                       x.StartsAt > from && x.StartsAt <= until).Limit(200).ToListAsync();

        public async Task<bool> TryClaimReminder(string sessionId, DateTimeOffset at)
        {
            var result = await Collection.UpdateOneAsync(x => x.SessionId == sessionId && x.ReminderSentAt == null,
                Builders<PaidSession>.Update.Set(x => x.ReminderSentAt, at));
            return result.ModifiedCount == 1;
        }

        public Task ReleaseReminder(string sessionId) =>
            Collection.UpdateOneAsync(x => x.SessionId == sessionId,
                Builders<PaidSession>.Update.Set(x => x.ReminderSentAt, null));

        public Task Update(PaidSession session) =>
            Collection.ReplaceOneAsync(x => x.SessionId == session.SessionId, session);
    }

    public class TicketOrderRepo : MongoRepo<TicketOrder>, IMongoIndexBuilder, IOrderRepo
    {
        static TicketOrderRepo()
        {
            BsonClassMap.RegisterClassMap<TicketOrder>(config =>
            {
                config.AutoMap();
                config.MapIdMember(x => x.OrderId);
                config.MapMember(x => x.Status).SetSerializer(new EnumSerializer<OrderStatus>(BsonType.String));
                config.MapMember(x => x.Kind).SetSerializer(new EnumSerializer<OrderKind>(BsonType.String));
                config.MapMember(x => x.CreatedAt).SetSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
                config.MapMember(x => x.PaidAt).SetSerializer(
                    new NullableSerializer<DateTimeOffset>(new DateTimeOffsetSerializer(BsonType.DateTime)));
                config.MapMember(x => x.RefundedAt).SetSerializer(
                    new NullableSerializer<DateTimeOffset>(new DateTimeOffsetSerializer(BsonType.DateTime)));
                config.SetIgnoreExtraElements(true);
            });
            BsonClassMap.RegisterClassMap<OrderAllocation>(config =>
            {
                config.AutoMap();
                config.SetIgnoreExtraElements(true);
            });
        }

        public TicketOrderRepo(IOptions<MongoDbOptions> options) : base(options)
        {
        }

        public async Task CreateIndexes()
        {
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<TicketOrder>(
                Builders<TicketOrder>.IndexKeys.Ascending(x => x.Reference), new CreateIndexOptions {Unique = true}));
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<TicketOrder>(
                Builders<TicketOrder>.IndexKeys.Ascending(x => x.BuyerUserId).Descending(x => x.CreatedAt)));
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<TicketOrder>(
                Builders<TicketOrder>.IndexKeys.Ascending(x => x.SessionId)));
        }

        public Task Create(TicketOrder order) => Collection.InsertOneAsync(order);

        public Task<TicketOrder?> FindByReference(string reference) =>
            Collection.Find(x => x.Reference == reference).FirstOrDefaultAsync()!;

        public async Task<bool> TryMarkPaid(string reference, DateTimeOffset paidAt, string? providerTransactionId,
            string? channel, IList<OrderAllocation> allocations)
        {
            // only a pending order changes: the first of two notifications at the same time wins
            var result = await Collection.UpdateOneAsync(
                x => x.Reference == reference && x.Status == OrderStatus.Pending,
                Builders<TicketOrder>.Update.Set(x => x.Status, OrderStatus.Paid).Set(x => x.PaidAt, paidAt)
                    .Set(x => x.ProviderTransactionId, providerTransactionId).Set(x => x.Channel, channel)
                    .Set(x => x.Allocations, allocations.ToList()));
            return result.ModifiedCount == 1;
        }

        public Task<TicketOrder?> FindById(string orderId) =>
            Collection.Find(x => x.OrderId == orderId).FirstOrDefaultAsync()!;

        public async Task<IReadOnlyList<TicketOrder>> FindRecent(int limit) =>
            await Collection.Find(FilterDefinition<TicketOrder>.Empty).SortByDescending(x => x.CreatedAt)
                .Limit(limit).ToListAsync();

        public async Task<IReadOnlyList<TicketOrder>> FindPaidSince(DateTimeOffset since, int limit) =>
            await Collection.Find(x => x.CreatedAt >= since &&
                                       (x.Status == OrderStatus.Paid || x.Status == OrderStatus.Refunded))
                .SortByDescending(x => x.CreatedAt).Limit(limit).ToListAsync();

        public async Task<IReadOnlyList<TicketOrder>> FindWithRefundFailure(int limit) =>
            await Collection.Find(x => x.Status == OrderStatus.Paid && x.RefundFailure != null)
                .SortByDescending(x => x.CreatedAt).Limit(limit).ToListAsync();

        public async Task<IReadOnlyList<TicketOrder>> FindPaidOfCreator(string creatorId, int limit) =>
            await Collection.Find(x => x.Shares.Any(s => s.CreatorId == creatorId) &&
                                       (x.Status == OrderStatus.Paid || x.Status == OrderStatus.Refunded))
                .SortByDescending(x => x.CreatedAt).Limit(limit).ToListAsync();

        public async Task<IReadOnlyList<TicketOrder>> FindPaidOfSession(string sessionId) =>
            await Collection.Find(x => x.SessionId == sessionId &&
                                       (x.Status == OrderStatus.Paid || x.Status == OrderStatus.Refunded))
                .ToListAsync();

        public async Task<bool> TryMarkRefunded(string reference, DateTimeOffset refundedAt, string? refundId)
        {
            var result = await Collection.UpdateOneAsync(
                x => x.Reference == reference && x.Status == OrderStatus.Paid,
                Builders<TicketOrder>.Update.Set(x => x.Status, OrderStatus.Refunded)
                    .Set(x => x.RefundedAt, refundedAt).Set(x => x.RefundId, refundId)
                    .Set(x => x.RefundFailure, null));
            return result.ModifiedCount == 1;
        }

        public Task RecordRefundFailure(string reference, string reason) =>
            Collection.UpdateOneAsync(x => x.Reference == reference && x.Status == OrderStatus.Paid,
                Builders<TicketOrder>.Update.Set(x => x.RefundFailure, reason));

        public Task MarkFailed(string reference, string reason) =>
            Collection.UpdateOneAsync(x => x.Reference == reference && x.Status == OrderStatus.Pending,
                Builders<TicketOrder>.Update.Set(x => x.Status, OrderStatus.Failed).Set(x => x.FailureReason, reason));
    }

    public class EntitlementRepo : MongoRepo<Entitlement>, IMongoIndexBuilder, IEntitlementRepo
    {
        static EntitlementRepo()
        {
            BsonClassMap.RegisterClassMap<Entitlement>(config =>
            {
                config.AutoMap();
                config.MapIdMember(x => x.EntitlementId);
                config.MapMember(x => x.GrantedAt).SetSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
                config.SetIgnoreExtraElements(true);
            });
        }

        public EntitlementRepo(IOptions<MongoDbOptions> options) : base(options)
        {
        }

        public async Task CreateIndexes()
        {
            // one right per paid order: this is what makes granting safe to repeat
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<Entitlement>(
                Builders<Entitlement>.IndexKeys.Ascending(x => x.OrderId), new CreateIndexOptions {Unique = true}));
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<Entitlement>(
                Builders<Entitlement>.IndexKeys.Ascending(x => x.UserId).Ascending(x => x.ConferenceId)));
        }

        public async Task<bool> GrantOnce(Entitlement entitlement)
        {
            try
            {
                await Collection.InsertOneAsync(entitlement);
                return true;
            }
            catch (MongoWriteException e) when (e.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                return false;
            }
        }

        public async Task<bool> Holds(string userId, string conferenceId) =>
            await Collection.Find(x => x.UserId == userId && x.ConferenceId == conferenceId && !x.Revoked)
                .AnyAsync();

        public async Task<IReadOnlyList<Entitlement>> FindOfUser(string userId, int limit) =>
            await Collection.Find(x => x.UserId == userId && !x.Revoked).SortByDescending(x => x.GrantedAt)
                .Limit(limit).ToListAsync();
    }

    public class PassRepo : MongoRepo<PassEntitlement>, IMongoIndexBuilder, IPassRepo
    {
        static PassRepo()
        {
            BsonClassMap.RegisterClassMap<PassEntitlement>(config =>
            {
                config.AutoMap();
                config.MapIdMember(x => x.PassId);
                config.MapMember(x => x.GrantedAt).SetSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
                config.MapMember(x => x.ValidUntil).SetSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
                config.SetIgnoreExtraElements(true);
            });
        }

        public PassRepo(IOptions<MongoDbOptions> options) : base(options)
        {
        }

        public async Task CreateIndexes()
        {
            // one pass per paid order: this is what makes granting safe to repeat
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<PassEntitlement>(
                Builders<PassEntitlement>.IndexKeys.Ascending(x => x.OrderId), new CreateIndexOptions {Unique = true}));
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<PassEntitlement>(
                Builders<PassEntitlement>.IndexKeys.Ascending(x => x.UserId).Ascending(x => x.CreatorId)
                    .Descending(x => x.ValidUntil)));
        }

        public async Task<PassEntitlement> GrantOnce(string userId, string creatorId, string orderId,
            DateTimeOffset now, int days)
        {
            var existing = await Collection.Find(x => x.OrderId == orderId).FirstOrDefaultAsync();
            if (existing != null) return existing;

            var start = await ActiveUntil(userId, creatorId, now) ?? now;
            var pass = new PassEntitlement(Guid.NewGuid().ToString("N"), userId, creatorId, orderId, now,
                start.AddDays(days));
            try
            {
                await Collection.InsertOneAsync(pass);
                return pass;
            }
            catch (MongoWriteException e) when (e.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                return await Collection.Find(x => x.OrderId == orderId).FirstAsync();
            }
        }

        public async Task<DateTimeOffset?> ActiveUntil(string userId, string creatorId, DateTimeOffset now)
        {
            var latest = await Collection.Find(x => x.UserId == userId && x.CreatorId == creatorId &&
                                                    x.ValidUntil > now)
                .SortByDescending(x => x.ValidUntil).FirstOrDefaultAsync();
            return latest?.ValidUntil;
        }

        public async Task<IReadOnlyList<PassEntitlement>> FindActiveOfUser(string userId, DateTimeOffset now) =>
            await Collection.Find(x => x.UserId == userId && x.ValidUntil > now)
                .SortByDescending(x => x.ValidUntil).Limit(100).ToListAsync();
    }

    public class PassSubscriptionRepo : MongoRepo<PassSubscription>, IMongoIndexBuilder, IPassSubscriptionRepo
    {
        static PassSubscriptionRepo()
        {
            BsonClassMap.RegisterClassMap<PassSubscription>(config =>
            {
                config.AutoMap();
                config.MapIdMember(x => x.SubscriptionId);
                config.MapMember(x => x.Status).SetSerializer(new EnumSerializer<SubscriptionStatus>(BsonType.String));
                config.MapMember(x => x.CreatedAt).SetSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
                config.MapMember(x => x.NextChargeAt).SetSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
                config.MapMember(x => x.NoticeSentFor).SetSerializer(
                    new NullableSerializer<DateTimeOffset>(new DateTimeOffsetSerializer(BsonType.DateTime)));
                config.MapMember(x => x.EndedAt).SetSerializer(
                    new NullableSerializer<DateTimeOffset>(new DateTimeOffsetSerializer(BsonType.DateTime)));
                config.SetIgnoreExtraElements(true);
            });
        }

        public PassSubscriptionRepo(IOptions<MongoDbOptions> options) : base(options)
        {
        }

        public async Task CreateIndexes()
        {
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<PassSubscription>(
                Builders<PassSubscription>.IndexKeys.Ascending(x => x.Status).Ascending(x => x.NextChargeAt)));
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<PassSubscription>(
                Builders<PassSubscription>.IndexKeys.Ascending(x => x.UserId).Ascending(x => x.CreatorId)));
        }

        public async Task<PassSubscription> StartOrReplace(PassSubscription subscription)
        {
            var existing = await Collection.Find(x => x.UserId == subscription.UserId &&
                                                       x.CreatorId == subscription.CreatorId &&
                                                       x.Status == SubscriptionStatus.Active).FirstOrDefaultAsync();
            if (existing == null)
            {
                await Collection.InsertOneAsync(subscription);
                return subscription;
            }

            existing.Email = subscription.Email;
            existing.AuthorizationCode = subscription.AuthorizationCode;
            existing.AmountMinor = subscription.AmountMinor;
            existing.NextChargeAt = subscription.NextChargeAt;
            existing.FailedAttempts = 0;
            existing.PendingReference = null;
            await Collection.ReplaceOneAsync(x => x.SubscriptionId == existing.SubscriptionId, existing);
            return existing;
        }

        public Task<PassSubscription?> FindById(string subscriptionId) =>
            Collection.Find(x => x.SubscriptionId == subscriptionId).FirstOrDefaultAsync()!;

        public async Task<int> CountActive() =>
            (int) await Collection.CountDocumentsAsync(x => x.Status == SubscriptionStatus.Active);

        public async Task<IReadOnlyList<PassSubscription>> FindWithProblems(DateTimeOffset endedSince, int limit)
        {
            var list = await Collection.Find(x =>
                    (x.Status == SubscriptionStatus.Active && x.FailedAttempts > 0) ||
                    (x.Status == SubscriptionStatus.Ended && x.EndedAt >= endedSince))
                .SortByDescending(x => x.NextChargeAt).Limit(limit).ToListAsync();
            return list;
        }

        public async Task<IReadOnlyList<PassSubscription>> FindActiveOfUser(string userId) =>
            await Collection.Find(x => x.UserId == userId && x.Status == SubscriptionStatus.Active).ToListAsync();

        public async Task<IReadOnlyList<PassSubscription>> FindDue(DateTimeOffset now, int limit) =>
            await Collection.Find(x => x.Status == SubscriptionStatus.Active && x.NextChargeAt <= now).Limit(limit)
                .ToListAsync();

        public async Task<IReadOnlyList<PassSubscription>> FindDueForNotice(DateTimeOffset until, int limit) =>
            await Collection.Find(x => x.Status == SubscriptionStatus.Active && x.NextChargeAt <= until &&
                                       (x.NoticeSentFor == null || x.NoticeSentFor != x.NextChargeAt))
                .Limit(limit).ToListAsync();

        public async Task<bool> TryStartCharge(string subscriptionId, DateTimeOffset now, DateTimeOffset leaseUntil)
        {
            var result = await Collection.UpdateOneAsync(
                x => x.SubscriptionId == subscriptionId && x.Status == SubscriptionStatus.Active &&
                     x.NextChargeAt <= now,
                Builders<PassSubscription>.Update.Set(x => x.NextChargeAt, leaseUntil));
            return result.ModifiedCount == 1;
        }

        public Task Update(PassSubscription subscription) =>
            Collection.ReplaceOneAsync(x => x.SubscriptionId == subscription.SubscriptionId, subscription);
    }
}
