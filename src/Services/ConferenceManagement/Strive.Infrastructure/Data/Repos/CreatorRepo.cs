using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Strive.Core.Services.Payments;
using Strive.Core.Services.Payments.Gateways;

namespace Strive.Infrastructure.Data.Repos
{
    public class CreatorRepo : MongoRepo<CreatorProfile>, IMongoIndexBuilder, ICreatorRepo
    {
        static CreatorRepo()
        {
            BsonClassMap.RegisterClassMap<CreatorProfile>(config =>
            {
                config.AutoMap();
                config.MapIdMember(x => x.CreatorId);
                config.MapMember(x => x.Status).SetSerializer(new EnumSerializer<CreatorStatus>(BsonType.String));
                config.MapMember(x => x.CreatedAt).SetSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
                config.MapMember(x => x.UpdatedAt).SetSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
                config.SetIgnoreExtraElements(true);
            });
        }

        public CreatorRepo(IOptions<MongoDbOptions> options) : base(options)
        {
        }

        public async Task CreateIndexes()
        {
            // a person has one creator profile
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<CreatorProfile>(
                Builders<CreatorProfile>.IndexKeys.Ascending(x => x.UserId), new CreateIndexOptions {Unique = true}));
        }

        public Task<CreatorProfile?> FindByUserId(string userId)
        {
            return Collection.Find(x => x.UserId == userId).FirstOrDefaultAsync()!;
        }

        public Task<CreatorProfile?> FindById(string creatorId)
        {
            return Collection.Find(x => x.CreatorId == creatorId).FirstOrDefaultAsync()!;
        }

        public Task Save(CreatorProfile profile)
        {
            return Collection.ReplaceOneAsync(x => x.CreatorId == profile.CreatorId, profile,
                new ReplaceOptions {IsUpsert = true});
        }
    }
}
