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
    public class VideoLikeRepo : MongoRepo<VideoLike>, IMongoIndexBuilder, IVideoLikeRepo
    {
        static VideoLikeRepo()
        {
            BsonClassMap.RegisterClassMap<VideoLike>(config =>
            {
                config.AutoMap();
                config.MapIdMember(x => x.LikeId);
                config.MapMember(x => x.CreatedAt).SetSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
                config.SetIgnoreExtraElements(true);
            });
        }

        public VideoLikeRepo(IOptions<MongoDbOptions> options) : base(options)
        {
        }

        public async Task CreateIndexes()
        {
            // a person likes a video once: this is what makes liking safe to repeat, and the count honest
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<VideoLike>(
                Builders<VideoLike>.IndexKeys.Ascending(x => x.VideoId).Ascending(x => x.UserId),
                new CreateIndexOptions {Unique = true}));
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<VideoLike>(
                Builders<VideoLike>.IndexKeys.Ascending(x => x.UserId)));
        }

        public async Task<bool> TryAdd(VideoLike like)
        {
            try
            {
                await Collection.InsertOneAsync(like);
                return true;
            }
            catch (MongoWriteException e) when (e.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                return false;
            }
        }

        public async Task<bool> Remove(string userId, string videoId) =>
            (await Collection.DeleteOneAsync(x => x.UserId == userId && x.VideoId == videoId)).DeletedCount > 0;

        public async Task<IReadOnlyDictionary<string, long>> CountsOf(IEnumerable<string> videoIds)
        {
            var ids = videoIds.Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<string, long>();

            var groups = await Collection.Aggregate().Match(Builders<VideoLike>.Filter.In(x => x.VideoId, ids))
                .Group(x => x.VideoId, g => new {VideoId = g.Key, Count = g.LongCount()}).ToListAsync();
            return groups.ToDictionary(x => x.VideoId, x => x.Count);
        }

        public async Task<IReadOnlySet<string>> LikedAmong(string userId, IEnumerable<string> videoIds)
        {
            var ids = videoIds.Distinct().ToList();
            var liked = await Collection.Find(Builders<VideoLike>.Filter.And(
                Builders<VideoLike>.Filter.Eq(x => x.UserId, userId),
                Builders<VideoLike>.Filter.In(x => x.VideoId, ids))).ToListAsync();
            return liked.Select(x => x.VideoId).ToHashSet();
        }

        public Task DeleteOfVideo(string videoId) => Collection.DeleteManyAsync(x => x.VideoId == videoId);
    }
}
