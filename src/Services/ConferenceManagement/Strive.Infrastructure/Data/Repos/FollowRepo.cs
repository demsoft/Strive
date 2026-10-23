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
                Builders<Follow>.IndexKeys.Ascending(x => x.CreatorId)));
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
