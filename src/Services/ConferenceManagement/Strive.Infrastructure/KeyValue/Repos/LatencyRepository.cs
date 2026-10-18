using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Strive.Core.Services;
using Strive.Core.Services.Latency;
using Strive.Core.Services.Latency.Gateways;
using Strive.Infrastructure.KeyValue.Abstractions;
using Strive.Infrastructure.KeyValue.Extensions;

namespace Strive.Infrastructure.KeyValue.Repos
{
    public class LatencyRepository : ILatencyRepository, IKeyValueRepo
    {
        private const string PROPERTY_KEY = "latency";

        private readonly IKeyValueDatabase _database;

        public LatencyRepository(IKeyValueDatabase database)
        {
            _database = database;
        }

        public async ValueTask<LatencyReport?> Set(Participant participant, LatencyReport report)
        {
            var key = GetKey(participant.ConferenceId);
            var before = await _database.HashGetAsync<LatencyReport>(key, participant.Id);
            await _database.HashSetAsync(key, participant.Id, report);
            return before;
        }

        public async ValueTask<bool> Remove(Participant participant)
        {
            return await _database.HashDeleteAsync(GetKey(participant.ConferenceId), participant.Id);
        }

        public async ValueTask<IReadOnlyDictionary<string, LatencyReport>> GetAll(string conferenceId)
        {
            var all = await _database.HashGetAllAsync<LatencyReport>(GetKey(conferenceId));
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
