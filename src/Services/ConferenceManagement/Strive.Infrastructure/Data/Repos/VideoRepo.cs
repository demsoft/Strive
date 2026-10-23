using System;
using System.Collections.Generic;
using System.Linq;
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
                config.MapMember(x => x.Moderation).SetSerializer(new EnumSerializer<VideoModeration>(BsonType.String));
                config.MapMember(x => x.ModeratedAt).SetSerializer(
                    new NullableSerializer<DateTimeOffset>(new DateTimeOffsetSerializer(BsonType.DateTime)));
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
            await Collection.Find(x => x.Status == VideoStatus.Ready && x.Moderation != VideoModeration.Hidden &&
                                       x.Moderation != VideoModeration.Removed && (before == null || x.ReadyAt < before))
                .SortByDescending(x => x.ReadyAt).Limit(limit).ToListAsync();

        public async Task<IReadOnlyList<Video>> FindReadyOfCreator(string creatorId, int limit) =>
            await Collection.Find(x => x.CreatorId == creatorId && x.Status == VideoStatus.Ready &&
                                       x.Moderation != VideoModeration.Hidden && x.Moderation != VideoModeration.Removed)
                .SortByDescending(x => x.ReadyAt).Limit(limit).ToListAsync();

        public async Task<IReadOnlyList<Video>> FindByIds(IEnumerable<string> videoIds)
        {
            var ids = videoIds.Distinct().ToList();
            return await Collection.Find(x => ids.Contains(x.VideoId)).ToListAsync();
        }

        public async Task<IReadOnlyList<Video>> FindModerated(int limit) =>
            await Collection.Find(x => x.Moderation == VideoModeration.Hidden || x.Moderation == VideoModeration.Removed)
                .SortByDescending(x => x.ModeratedAt).Limit(limit).ToListAsync();

        public async Task<IReadOnlyList<Video>> FindByStatus(VideoStatus status, int limit) =>
            await Collection.Find(x => x.Status == status).SortBy(x => x.CreatedAt).Limit(limit).ToListAsync();

        public Task Update(Video video) => Collection.ReplaceOneAsync(x => x.VideoId == video.VideoId, video);

        public Task Delete(string videoId) => Collection.DeleteOneAsync(x => x.VideoId == videoId);
    }

    public class VideoEntitlementRepo : MongoRepo<VideoEntitlement>, IMongoIndexBuilder, IVideoEntitlementRepo
    {
        static VideoEntitlementRepo()
        {
            BsonClassMap.RegisterClassMap<VideoEntitlement>(config =>
            {
                config.AutoMap();
                config.MapIdMember(x => x.EntitlementId);
                config.MapMember(x => x.GrantedAt).SetSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
                config.SetIgnoreExtraElements(true);
            });
        }

        public VideoEntitlementRepo(IOptions<MongoDbOptions> options) : base(options)
        {
        }

        public async Task CreateIndexes()
        {
            // one right per paid order: this is what makes granting safe to repeat
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<VideoEntitlement>(
                Builders<VideoEntitlement>.IndexKeys.Ascending(x => x.OrderId), new CreateIndexOptions {Unique = true}));
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<VideoEntitlement>(
                Builders<VideoEntitlement>.IndexKeys.Ascending(x => x.UserId).Ascending(x => x.VideoId)));
        }

        public async Task<bool> GrantOnce(VideoEntitlement entitlement)
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

        public async Task<bool> Holds(string userId, string videoId) =>
            await Collection.Find(x => x.UserId == userId && x.VideoId == videoId).AnyAsync();

        public async Task<IReadOnlyList<string>> HeldAmong(string userId, IEnumerable<string> videoIds)
        {
            var ids = videoIds.ToList();
            return (await Collection.Find(x => x.UserId == userId && ids.Contains(x.VideoId)).ToListAsync())
                .Select(x => x.VideoId).Distinct().ToList();
        }

        public async Task<IReadOnlyList<VideoEntitlement>> FindOfUser(string userId, int limit) =>
            await Collection.Find(x => x.UserId == userId).SortByDescending(x => x.GrantedAt).Limit(limit).ToListAsync();

        public Task<long> CountOfVideo(string videoId) => Collection.CountDocumentsAsync(x => x.VideoId == videoId);
    }

    public class VideoReportRepo : MongoRepo<VideoReport>, IMongoIndexBuilder, IVideoReportRepo
    {
        static VideoReportRepo()
        {
            BsonClassMap.RegisterClassMap<VideoReport>(config =>
            {
                config.AutoMap();
                config.MapIdMember(x => x.ReportId);
                config.MapMember(x => x.Reason).SetSerializer(new EnumSerializer<ReportReason>(BsonType.String));
                config.MapMember(x => x.CreatedAt).SetSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
                config.MapMember(x => x.ResolvedAt).SetSerializer(
                    new NullableSerializer<DateTimeOffset>(new DateTimeOffsetSerializer(BsonType.DateTime)));
                config.SetIgnoreExtraElements(true);
            });
        }

        public VideoReportRepo(IOptions<MongoDbOptions> options) : base(options)
        {
        }

        public async Task CreateIndexes()
        {
            // a person reports a video once: this is what stops one person from hiding it alone
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<VideoReport>(
                Builders<VideoReport>.IndexKeys.Ascending(x => x.VideoId).Ascending(x => x.ReporterUserId),
                new CreateIndexOptions {Unique = true}));
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<VideoReport>(
                Builders<VideoReport>.IndexKeys.Ascending(x => x.Resolved).Descending(x => x.CreatedAt)));
        }

        public async Task<bool> TryAdd(VideoReport report)
        {
            try
            {
                await Collection.InsertOneAsync(report);
                return true;
            }
            catch (MongoWriteException e) when (e.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                return false;
            }
        }

        public async Task<int> CountOpen(string videoId) =>
            (int) await Collection.CountDocumentsAsync(x => x.VideoId == videoId && !x.Resolved);

        public async Task<IReadOnlyList<VideoReport>> FindOpen(int limit) =>
            await Collection.Find(x => !x.Resolved).SortByDescending(x => x.CreatedAt).Limit(limit).ToListAsync();

        public async Task<IReadOnlyList<VideoReport>> FindOpenOfVideo(string videoId) =>
            await Collection.Find(x => x.VideoId == videoId && !x.Resolved).SortByDescending(x => x.CreatedAt)
                .ToListAsync();

        public Task ResolveOpen(string videoId, DateTimeOffset at) =>
            Collection.UpdateManyAsync(x => x.VideoId == videoId && !x.Resolved,
                Builders<VideoReport>.Update.Set(x => x.Resolved, true).Set(x => x.ResolvedAt, at));
    }
}
