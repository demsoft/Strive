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
                config.MapMember(x => x.CreatedAt).SetSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
                config.MapMember(x => x.PaidAt).SetSerializer(
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
}
