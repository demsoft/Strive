using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Strive.Core.Services.Recording;
using Strive.Core.Services.Recording.Gateways;

namespace Strive.Infrastructure.Data.Repos
{
    public class RecordingRepo : MongoRepo<ConferenceRecording>, IMongoIndexBuilder, IRecordingRepo
    {
        private static readonly RecordingStatus[] ActiveStatuses =
        {
            RecordingStatus.Starting, RecordingStatus.Recording, RecordingStatus.Finalizing,
        };

        static RecordingRepo()
        {
            BsonClassMap.RegisterClassMap<ConferenceRecording>(config =>
            {
                config.AutoMap();
                config.MapIdMember(x => x.RecordingId);
                config.MapMember(x => x.IsActive);
                config.MapMember(x => x.Status).SetSerializer(new EnumSerializer<RecordingStatus>(BsonType.String));
                config.MapMember(x => x.Visibility)
                    .SetSerializer(new EnumSerializer<RecordingVisibility>(BsonType.String));
                // stored as real dates (the default is a tick and offset document) so that they can be queried
                config.MapMember(x => x.StartedAt).SetSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
                config.MapMember(x => x.EndedAt).SetSerializer(
                    new NullableSerializer<DateTimeOffset>(new DateTimeOffsetSerializer(BsonType.DateTime)));
                config.MapMember(x => x.ExpiresAt).SetSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
                config.SetIgnoreExtraElements(true);
            });
        }

        public RecordingRepo(IOptions<MongoDbOptions> options) : base(options)
        {
        }

        public async Task CreateIndexes()
        {
            // there is at most one unfinished recording per conference
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<ConferenceRecording>(
                Builders<ConferenceRecording>.IndexKeys.Ascending(x => x.ConferenceId), new CreateIndexOptions<ConferenceRecording>
                {
                    Unique = true,
                    Name = "one_active_recording_per_conference",
                    // only equality is supported by every MongoDB version in partial indexes
                    PartialFilterExpression = new BsonDocument("IsActive", true),
                }));

            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<ConferenceRecording>(
                Builders<ConferenceRecording>.IndexKeys.Ascending(x => x.ShareToken),
                new CreateIndexOptions {Unique = true}));
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<ConferenceRecording>(
                Builders<ConferenceRecording>.IndexKeys.Ascending(x => x.ExpiresAt)));
        }

        public async Task<bool> TryCreate(ConferenceRecording recording)
        {
            try
            {
                await Collection.InsertOneAsync(recording);
                return true;
            }
            catch (MongoWriteException e) when (e.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                return false;
            }
        }

        public Task<ConferenceRecording?> FindById(string recordingId)
        {
            return Collection.Find(x => x.RecordingId == recordingId).FirstOrDefaultAsync()!;
        }

        public Task<ConferenceRecording?> FindByShareToken(string shareToken)
        {
            return Collection.Find(x => x.ShareToken == shareToken).FirstOrDefaultAsync()!;
        }

        public Task<ConferenceRecording?> FindActiveOfConference(string conferenceId)
        {
            return Collection.Find(Builders<ConferenceRecording>.Filter.And(
                    Builders<ConferenceRecording>.Filter.Eq(x => x.ConferenceId, conferenceId),
                    Builders<ConferenceRecording>.Filter.Eq(x => x.IsActive, true))).FirstOrDefaultAsync()!;
        }

        public async Task<IReadOnlyList<ConferenceRecording>> FindOfConference(string conferenceId)
        {
            return await Collection.Find(x => x.ConferenceId == conferenceId)
                .SortByDescending(x => x.StartedAt).ToListAsync();
        }

        public async Task<IReadOnlyList<ConferenceRecording>> FindStartedBy(string participantId, int limit)
        {
            return await Collection.Find(x => x.StartedBy == participantId)
                .SortByDescending(x => x.StartedAt).Limit(limit).ToListAsync();
        }

        public async Task<IReadOnlyList<ConferenceRecording>> FindExpired(DateTimeOffset now)
        {
            // an unfinished recording is never deleted while its recorder may still upload
            return await Collection.Find(Builders<ConferenceRecording>.Filter.And(
                Builders<ConferenceRecording>.Filter.Lte(x => x.ExpiresAt, now),
                Builders<ConferenceRecording>.Filter.Nin(x => x.Status, ActiveStatuses))).ToListAsync();
        }

        public async Task<IReadOnlyList<ConferenceRecording>> FindUnfinishedStartedBefore(DateTimeOffset date)
        {
            return await Collection.Find(Builders<ConferenceRecording>.Filter.And(
                Builders<ConferenceRecording>.Filter.Lt(x => x.StartedAt, date),
                Builders<ConferenceRecording>.Filter.In(x => x.Status, ActiveStatuses))).ToListAsync();
        }

        public Task Update(ConferenceRecording recording)
        {
            return Collection.ReplaceOneAsync(x => x.RecordingId == recording.RecordingId, recording);
        }

        public Task Delete(string recordingId)
        {
            return Collection.DeleteOneAsync(x => x.RecordingId == recordingId);
        }
    }
}
