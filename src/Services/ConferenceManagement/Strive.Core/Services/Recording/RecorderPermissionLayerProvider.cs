using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Strive.Core.Services.Permissions;

namespace Strive.Core.Services.Recording
{
    /// <summary>
    ///     The recorder only receives: every permission is denied for it, whatever the conference grants
    /// </summary>
    public class RecorderPermissionLayerProvider : IPermissionLayerProvider
    {
        public const int Order = 1000;

        public ValueTask<IEnumerable<PermissionLayer>> FetchPermissionsForParticipant(Participant participant)
        {
            if (!RecorderParticipants.IsRecorder(participant.Id))
                return new ValueTask<IEnumerable<PermissionLayer>>(Enumerable.Empty<PermissionLayer>());

            var denied = DefinedPermissionsProvider.All.Values.Where(x => x.Type == PermissionValueType.Boolean)
                .ToDictionary(x => x.Key, _ => new JValue(false));

            IEnumerable<PermissionLayer> result = new[] {new PermissionLayer(Order, "RECORDER", denied)};
            return new ValueTask<IEnumerable<PermissionLayer>>(result);
        }
    }
}
