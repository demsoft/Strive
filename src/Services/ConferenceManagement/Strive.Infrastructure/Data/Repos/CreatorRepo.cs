using System.Linq;
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

            // a slug belongs to one creator; most creators have none, so only the ones that are set count
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<CreatorProfile>(
                Builders<CreatorProfile>.IndexKeys.Ascending(x => x.Slug),
                new CreateIndexOptions<CreatorProfile>
                {
                    Unique = true,
                    PartialFilterExpression = Builders<CreatorProfile>.Filter.Type(x => x.Slug, BsonType.String),
                }));
        }

        public Task<long> Count() => Collection.CountDocumentsAsync(FilterDefinition<CreatorProfile>.Empty);

        public async Task<System.Collections.Generic.IReadOnlyDictionary<string, string>> FindNames(
            System.Collections.Generic.IEnumerable<string> creatorIds)
        {
            var ids = creatorIds.Distinct().ToList();
            var found = await Collection.Find(x => ids.Contains(x.CreatorId)).ToListAsync();
            return found.ToDictionary(x => x.CreatorId, x => x.DisplayName);
        }

        public async Task<System.Collections.Generic.IReadOnlyList<CreatorProfile>> FindByIds(
            System.Collections.Generic.IEnumerable<string> creatorIds)
        {
            var ids = creatorIds.Distinct().ToList();
            return await Collection.Find(x => ids.Contains(x.CreatorId)).ToListAsync();
        }

        public Task<CreatorProfile?> FindBySlug(string slug)
        {
            return Collection.Find(x => x.Slug == slug).FirstOrDefaultAsync()!;
        }

        public Task<CreatorProfile?> FindByUserId(string userId)
        {
            return Collection.Find(x => x.UserId == userId).FirstOrDefaultAsync()!;
        }

        public Task<CreatorProfile?> FindById(string creatorId)
        {
            return Collection.Find(x => x.CreatorId == creatorId).FirstOrDefaultAsync()!;
        }

        public async Task Save(CreatorProfile profile)
        {
            try
            {
                await Collection.ReplaceOneAsync(x => x.CreatorId == profile.CreatorId, profile,
                    new ReplaceOptions {IsUpsert = true});
            }
            catch (MongoWriteException e) when (e.WriteError.Category == ServerErrorCategory.DuplicateKey &&
                                                profile.Slug != null)
            {
                throw new SlugTakenException();
            }
        }
    }

    public class CreatorPhotoRepo : MongoRepo<CreatorPhoto>, ICreatorPhotoRepo
    {
        static CreatorPhotoRepo()
        {
            BsonClassMap.RegisterClassMap<CreatorPhoto>(config =>
            {
                config.AutoMap();
                config.MapIdMember(x => x.CreatorId);
                config.MapMember(x => x.UpdatedAt).SetSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
                config.SetIgnoreExtraElements(true);
            });
        }

        public CreatorPhotoRepo(IOptions<MongoDbOptions> options) : base(options)
        {
        }

        public Task<CreatorPhoto?> Find(string creatorId) =>
            Collection.Find(x => x.CreatorId == creatorId).FirstOrDefaultAsync()!;

        public Task Save(CreatorPhoto photo) =>
            Collection.ReplaceOneAsync(x => x.CreatorId == photo.CreatorId, photo, new ReplaceOptions {IsUpsert = true});

        public Task Delete(string creatorId) => Collection.DeleteOneAsync(x => x.CreatorId == creatorId);
    }
}
