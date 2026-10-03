using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Strive.Core.Services;
using Strive.Core.Services.HandRaise.Gateways;
using Strive.Infrastructure.KeyValue.Abstractions;
using Strive.Infrastructure.KeyValue.Extensions;

namespace Strive.Infrastructure.KeyValue.Repos
{
    public class HandRaiseRepository : IHandRaiseRepository, IKeyValueRepo
    {
        private const string PROPERTY_KEY = "handRaises";

        private readonly IKeyValueDatabase _database;

        public HandRaiseRepository(IKeyValueDatabase database)
        {
            _database = database;
        }

        public async ValueTask<bool> Raise(Participant participant, DateTimeOffset raisedAt)
        {
            var key = GetKey(participant.ConferenceId);

            if (await _database.HashExistsAsync(key, participant.Id)) return false;

            await _database.HashSetAsync(key, participant.Id, raisedAt);
            return true;
        }

        public async ValueTask<bool> Lower(Participant participant)
        {
            return await _database.HashDeleteAsync(GetKey(participant.ConferenceId), participant.Id);
        }

        public async ValueTask<IReadOnlyDictionary<string, DateTimeOffset>> GetAll(string conferenceId)
        {
            var all = await _database.HashGetAllAsync<DateTimeOffset>(GetKey(conferenceId));
            return all.ToDictionary(x => x.Key, x => x.Value);
        }

        public async ValueTask RemoveAllDataOfConference(string conferenceId)
        {
            await _database.KeyDeleteAsync(GetKey(conferenceId));
        }

        private static string GetKey(string conferenceId)
        {
            return DatabaseKeyBuilder.ForProperty(PROPERTY_KEY).ForConference(conferenceId).ToString();
        }
    }
}
