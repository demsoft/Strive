using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Strive.Core.Services.Social;

namespace Strive.Infrastructure.Data.Repos
{
    public class FollowRepo : MongoRepo<Follow>, IMongoIndexBuilder, IFollowRepo
    {
        static FollowRepo()
        {
            BsonClassMap.RegisterClassMap<Follow>(config =>
            {
                config.AutoMap();
                config.MapIdMember(x => x.FollowId);
                config.MapMember(x => x.CreatedAt).SetSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
                // a follow without notices has no token at all (the index of the tokens is unique)
                config.MapMember(x => x.NoticeToken).SetIgnoreIfNull(true);
                config.MapMember(x => x.NoticeEmail).SetIgnoreIfNull(true);
                config.MapMember(x => x.Notice).SetSerializer(new EnumSerializer<NoticeStatus>(BsonType.String));
                config.MapMember(x => x.LastNoticeAt).SetSerializer(
                    new NullableSerializer<DateTimeOffset>(new DateTimeOffsetSerializer(BsonType.DateTime)));
                config.SetIgnoreExtraElements(true);
            });
        }

        public FollowRepo(IOptions<MongoDbOptions> options) : base(options)
        {
        }

        public async Task CreateIndexes()
        {
            // a person follows a creator once: this is what makes following safe to repeat
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<Follow>(
                Builders<Follow>.IndexKeys.Ascending(x => x.UserId).Ascending(x => x.CreatorId),
                new CreateIndexOptions {Unique = true}));
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<Follow>(
                Builders<Follow>.IndexKeys.Ascending(x => x.CreatorId).Ascending(x => x.Notice)));
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<Follow>(
                Builders<Follow>.IndexKeys.Ascending(x => x.NoticeToken),
                new CreateIndexOptions<Follow>
                {
                    Unique = true, PartialFilterExpression = Builders<Follow>.Filter.Type(x => x.NoticeToken, BsonType.String),
                }));
        }

        public async Task<bool> TryAdd(Follow follow)
        {
            try
            {
                await Collection.InsertOneAsync(follow);
                return true;
            }
            catch (MongoWriteException e) when (e.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                return false;
            }
        }

        public Task<Follow?> Find(string userId, string creatorId) =>
            Collection.Find(x => x.UserId == userId && x.CreatorId == creatorId).FirstOrDefaultAsync()!;

        public Task<Follow?> FindByNoticeToken(string token) =>
            Collection.Find(x => x.NoticeToken == token).FirstOrDefaultAsync()!;

        public Task SetNotice(string followId, NoticeStatus status, string? email, string? token)
        {
            var update = Builders<Follow>.Update.Set(x => x.Notice, status);
            update = email == null ? update.Unset(x => x.NoticeEmail) : update.Set(x => x.NoticeEmail, email);
            update = token == null ? update.Unset(x => x.NoticeToken) : update.Set(x => x.NoticeToken, token);
            return Collection.UpdateOneAsync(x => x.FollowId == followId, update);
        }

        public async Task<IReadOnlyList<Follow>> FindNoticeRecipients(string creatorId, DateTimeOffset notNoticedSince,
            int limit) =>
            await Collection.Find(x => x.CreatorId == creatorId && x.Notice == NoticeStatus.On &&
                                       (x.LastNoticeAt == null || x.LastNoticeAt < notNoticedSince)).Limit(limit)
                .ToListAsync();

        public Task MarkNoticed(IEnumerable<string> followIds, DateTimeOffset at)
        {
            var ids = followIds.ToList();
            return Collection.UpdateManyAsync(x => ids.Contains(x.FollowId),
                Builders<Follow>.Update.Set(x => x.LastNoticeAt, at));
        }

        public async Task<bool> Remove(string userId, string creatorId) =>
            (await Collection.DeleteOneAsync(x => x.UserId == userId && x.CreatorId == creatorId)).DeletedCount > 0;

        public async Task<bool> Exists(string userId, string creatorId) =>
            await Collection.Find(x => x.UserId == userId && x.CreatorId == creatorId).AnyAsync();

        public Task<long> CountOfCreator(string creatorId) => Collection.CountDocumentsAsync(x => x.CreatorId == creatorId);

        public async Task<IReadOnlyList<string>> CreatorIdsOfUser(string userId, int limit) =>
            (await Collection.Find(x => x.UserId == userId).SortByDescending(x => x.CreatedAt).Limit(limit)
                .ToListAsync()).Select(x => x.CreatorId).ToList();
    }
}
