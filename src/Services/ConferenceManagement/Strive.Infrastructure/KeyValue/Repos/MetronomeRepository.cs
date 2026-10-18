using System.Threading.Tasks;
using Strive.Core.Services.Metronome;
using Strive.Core.Services.Metronome.Gateways;
using Strive.Infrastructure.KeyValue.Abstractions;
using Strive.Infrastructure.KeyValue.Extensions;

namespace Strive.Infrastructure.KeyValue.Repos
{
    public class MetronomeRepository : IMetronomeRepository, IKeyValueRepo
    {
        private const string PROPERTY_KEY = "metronome";

        private readonly IKeyValueDatabase _database;

        public MetronomeRepository(IKeyValueDatabase database)
        {
            _database = database;
        }

        public async ValueTask<MetronomeSession?> Get(string conferenceId)
        {
            return await _database.GetAsync<MetronomeSession>(GetKey(conferenceId));
        }

        public async ValueTask Set(string conferenceId, MetronomeSession session)
        {
            await _database.SetAsync(GetKey(conferenceId), session);
        }

        public async ValueTask<bool> Clear(string conferenceId)
        {
            return await _database.KeyDeleteAsync(GetKey(conferenceId));
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
