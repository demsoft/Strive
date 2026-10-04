using System.Threading.Tasks;
using Strive.Core.Services;
using Strive.Core.Services.WatchTogether;
using Strive.Core.Services.WatchTogether.Gateways;
using Strive.Infrastructure.KeyValue.Abstractions;
using Strive.Infrastructure.KeyValue.Extensions;

namespace Strive.Infrastructure.KeyValue.Repos
{
    public class WatchTogetherRepository : IWatchTogetherRepository, IKeyValueRepo
    {
        private const string PROPERTY_KEY = "watchTogether";

        private readonly IKeyValueDatabase _database;

        public WatchTogetherRepository(IKeyValueDatabase database)
        {
            _database = database;
        }

        public async ValueTask<WatchTogetherSession?> Get(string conferenceId)
        {
            return await _database.GetAsync<WatchTogetherSession>(GetKey(conferenceId));
        }

        public async ValueTask Set(string conferenceId, WatchTogetherSession session)
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
