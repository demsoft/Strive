using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Strive.Core.Services;
using Strive.Core.Services.Lobby;
using Strive.Core.Services.Lobby.Gateways;
using Strive.Infrastructure.KeyValue.Abstractions;
using Strive.Infrastructure.KeyValue.Extensions;

namespace Strive.Infrastructure.KeyValue.Repos
{
    public class LobbyRepository : ILobbyRepository, IKeyValueRepo
    {
        private const string WAITING_KEY = "lobbyWaiting";
        private const string ADMITTED_KEY = "lobbyAdmitted";

        private readonly IKeyValueDatabase _database;

        public LobbyRepository(IKeyValueDatabase database)
        {
            _database = database;
        }

        public async ValueTask Add(Participant participant, LobbyEntry entry)
        {
            await _database.HashSetAsync(GetKey(WAITING_KEY, participant.ConferenceId), participant.Id, entry);
        }

        public async ValueTask<LobbyEntry?> TryRemove(Participant participant, string? expectedConnectionId = null)
        {
            var key = GetKey(WAITING_KEY, participant.ConferenceId);

            var entry = await _database.HashGetAsync<LobbyEntry>(key, participant.Id);
            if (entry == null) return null;
            if (expectedConnectionId != null && entry.ConnectionId != expectedConnectionId) return null;

            // if multiple callers try to remove the entry, only the one that actually deleted it wins
            return await _database.HashDeleteAsync(key, participant.Id) ? entry : null;
        }

        public async ValueTask<IReadOnlyDictionary<string, LobbyEntry>> GetAll(string conferenceId)
        {
            var all = await _database.HashGetAllAsync<LobbyEntry>(GetKey(WAITING_KEY, conferenceId));
            return all.Where(x => x.Value != null).ToDictionary(x => x.Key, x => x.Value!);
        }

        public async ValueTask MarkAdmitted(Participant participant)
        {
            await _database.HashSetAsync(GetKey(ADMITTED_KEY, participant.ConferenceId), participant.Id, true);
        }

        public async ValueTask RemoveAdmitted(Participant participant)
        {
            await _database.HashDeleteAsync(GetKey(ADMITTED_KEY, participant.ConferenceId), participant.Id);
        }

        public async ValueTask<bool> IsAdmitted(Participant participant)
        {
            return await _database.HashExistsAsync(GetKey(ADMITTED_KEY, participant.ConferenceId), participant.Id);
        }

        public async ValueTask RemoveAllDataOfConference(string conferenceId)
        {
            await _database.KeyDeleteAsync(GetKey(WAITING_KEY, conferenceId));
            await _database.KeyDeleteAsync(GetKey(ADMITTED_KEY, conferenceId));
        }

        private static string GetKey(string property, string conferenceId)
        {
            return DatabaseKeyBuilder.ForProperty(property).ForConference(conferenceId).ToString();
        }
    }
}
