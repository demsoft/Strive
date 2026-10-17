using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Strive.Core.Extensions;
using Strive.Core.Services.LessonMode.Gateways;
using Strive.Core.Services.Permissions;

namespace Strive.Core.Services.LessonMode
{
    /// <summary>
    ///     While the lesson mode is on, nobody can share audio (microphone and instrument) except those whom a higher layer
    ///     allows it: the moderators, and the participants who got a temporary permission to speak.
    /// </summary>
    public class LessonModePermissionLayerProvider : IPermissionLayerProvider
    {
        /// <summary>Above the conference permissions, below the moderators and the temporary permissions</summary>
        public const int LayerOrder = 12;

        private readonly ILessonModeRepository _repository;

        public LessonModePermissionLayerProvider(ILessonModeRepository repository)
        {
            _repository = repository;
        }

        public async ValueTask<IEnumerable<PermissionLayer>> FetchPermissionsForParticipant(Participant participant)
        {
            if (!await _repository.IsActive(participant.ConferenceId)) return new List<PermissionLayer>();

            var permissions = new[] {DefinedPermissions.Media.CanShareAudio.Configure(false)}.ToImmutableDictionary();
            return new List<PermissionLayer> {new(LayerOrder, "LESSON_MODE", permissions)};
        }
    }
}
