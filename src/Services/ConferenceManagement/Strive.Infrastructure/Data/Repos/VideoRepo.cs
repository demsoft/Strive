using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Strive.Core.Services.Videos;

namespace Strive.Infrastructure.Data.Repos
{
    public class VideoRepo : MongoRepo<Video>, IMongoIndexBuilder, IVideoRepo
    {
        static VideoRepo()
        {
            BsonClassMap.RegisterClassMap<Video>(config =>
            {
                config.AutoMap();
                config.MapIdMember(x => x.VideoId);
                config.MapMember(x => x.Status).SetSerializer(new EnumSerializer<VideoStatus>(BsonType.String));
                config.MapMember(x => x.CreatedAt).SetSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
                config.MapMember(x => x.ProcessingSince).SetSerializer(
                    new NullableSerializer<DateTimeOffset>(new DateTimeOffsetSerializer(BsonType.DateTime)));
                config.MapMember(x => x.ReadyAt).SetSerializer(
                    new NullableSerializer<DateTimeOffset>(new DateTimeOffsetSerializer(BsonType.DateTime)));
                config.SetIgnoreExtraElements(true);
            });
        }

        public VideoRepo(IOptions<MongoDbOptions> options) : base(options)
        {
        }

        public async Task CreateIndexes()
        {
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<Video>(
                Builders<Video>.IndexKeys.Ascending(x => x.CreatorId).Descending(x => x.CreatedAt)));
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<Video>(
                Builders<Video>.IndexKeys.Ascending(x => x.Status).Ascending(x => x.CreatedAt)));
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<Video>(
                Builders<Video>.IndexKeys.Ascending(x => x.Status).Descending(x => x.ReadyAt)));
        }

        public Task Create(Video video) => Collection.InsertOneAsync(video);

        public Task<Video?> FindById(string videoId) =>
            Collection.Find(x => x.VideoId == videoId).FirstOrDefaultAsync()!;

        public async Task<IReadOnlyList<Video>> FindOfCreator(string creatorId, int limit) =>
            await Collection.Find(x => x.CreatorId == creatorId).SortByDescending(x => x.CreatedAt).Limit(limit)
                .ToListAsync();

        public Task<long> CountOfCreator(string creatorId) =>
            Collection.CountDocumentsAsync(x => x.CreatorId == creatorId);

        public async Task<IReadOnlyList<Video>> FindReady(DateTimeOffset? before, int limit) =>
            await Collection.Find(x => x.Status == VideoStatus.Ready && (before == null || x.ReadyAt < before))
                .SortByDescending(x => x.ReadyAt).Limit(limit).ToListAsync();

        public async Task<IReadOnlyList<Video>> FindReadyOfCreator(string creatorId, int limit) =>
            await Collection.Find(x => x.CreatorId == creatorId && x.Status == VideoStatus.Ready)
                .SortByDescending(x => x.ReadyAt).Limit(limit).ToListAsync();

        public async Task<IReadOnlyList<Video>> FindByStatus(VideoStatus status, int limit) =>
            await Collection.Find(x => x.Status == status).SortBy(x => x.CreatedAt).Limit(limit).ToListAsync();

        public Task Update(Video video) => Collection.ReplaceOneAsync(x => x.VideoId == video.VideoId, video);

        public Task Delete(string videoId) => Collection.DeleteOneAsync(x => x.VideoId == videoId);
    }
}
