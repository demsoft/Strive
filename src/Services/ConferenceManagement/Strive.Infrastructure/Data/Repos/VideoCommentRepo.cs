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
    public class VideoCommentRepo : MongoRepo<VideoComment>, IMongoIndexBuilder, IVideoCommentRepo
    {
        static VideoCommentRepo()
        {
            BsonClassMap.RegisterClassMap<VideoComment>(config =>
            {
                config.AutoMap();
                config.MapIdMember(x => x.CommentId);
                config.MapMember(x => x.CreatedAt).SetSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
                config.SetIgnoreExtraElements(true);
            });
        }

        public VideoCommentRepo(IOptions<MongoDbOptions> options) : base(options)
        {
        }

        public async Task CreateIndexes()
        {
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<VideoComment>(
                Builders<VideoComment>.IndexKeys.Ascending(x => x.VideoId).Descending(x => x.CreatedAt)));
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<VideoComment>(
                Builders<VideoComment>.IndexKeys.Ascending(x => x.UserId).Descending(x => x.CreatedAt)));
        }

        public Task Add(VideoComment comment) => Collection.InsertOneAsync(comment);

        public Task<VideoComment?> FindById(string commentId) =>
            Collection.Find(x => x.CommentId == commentId).FirstOrDefaultAsync()!;

        public async Task<IReadOnlyList<VideoComment>> FindOfVideo(string videoId, DateTimeOffset? before, int limit)
        {
            var filter = Builders<VideoComment>.Filter.Eq(x => x.VideoId, videoId);
            if (before != null) filter &= Builders<VideoComment>.Filter.Lt(x => x.CreatedAt, before.Value);
            return await Collection.Find(filter).SortByDescending(x => x.CreatedAt).Limit(limit).ToListAsync();
        }

        public Task Delete(string commentId) => Collection.DeleteOneAsync(x => x.CommentId == commentId);

        public Task DeleteOfVideo(string videoId) => Collection.DeleteManyAsync(x => x.VideoId == videoId);

        public async Task<IReadOnlyDictionary<string, long>> CountsOf(IEnumerable<string> videoIds)
        {
            var ids = videoIds.Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<string, long>();

            var groups = await Collection.Aggregate().Match(Builders<VideoComment>.Filter.In(x => x.VideoId, ids))
                .Group(x => x.VideoId, g => new {VideoId = g.Key, Count = g.LongCount()}).ToListAsync();
            return groups.ToDictionary(x => x.VideoId, x => x.Count);
        }

        public Task<long> CountSince(string userId, DateTimeOffset since) =>
            Collection.CountDocumentsAsync(Builders<VideoComment>.Filter.And(
                Builders<VideoComment>.Filter.Eq(x => x.UserId, userId),
                Builders<VideoComment>.Filter.Gte(x => x.CreatedAt, since)));
    }
}
