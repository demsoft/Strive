using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Strive.Core.Extensions;
using Strive.Core.Services.Lobby.Gateways;
using Strive.Core.Services.Permissions;
using Strive.Core.Services.Synchronization;

namespace Strive.Core.Services.Lobby
{
    public class SynchronizedLobbyProvider : SynchronizedObjectProvider<SynchronizedLobby>
    {
        private readonly ILobbyRepository _repository;
        private readonly IParticipantPermissions _participantPermissions;

        public SynchronizedLobbyProvider(ILobbyRepository repository, IParticipantPermissions participantPermissions)
        {
            _repository = repository;
            _participantPermissions = participantPermissions;
        }

        public override string Id => SynchronizedObjectIds.LOBBY;

        public override async ValueTask<IEnumerable<SynchronizedObjectId>> GetAvailableObjects(Participant participant)
        {
            var permissions = await _participantPermissions.FetchForParticipant(participant);
            if (!await permissions.GetPermissionValue(DefinedPermissions.Lobby.CanAdmit))
                return Enumerable.Empty<SynchronizedObjectId>();

            return SynchronizedLobby.SyncObjId.Yield();
        }

        protected override async ValueTask<SynchronizedLobby> InternalFetchValue(string conferenceId,
            SynchronizedObjectId synchronizedObjectId)
        {
            var entries = await _repository.GetAll(conferenceId);
            return new SynchronizedLobby(entries.ToDictionary(x => x.Key,
                x => new SynchronizedLobby.Waiting(x.Value.DisplayName, x.Value.Since)));
        }
    }
}
