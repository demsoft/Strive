using System.Collections.Generic;
using System.Threading.Tasks;
using Strive.Core.Interfaces.Gateways;

namespace Strive.Core.Services.Latency.Gateways
{
    public interface ILatencyRepository : IStateRepository
    {
        /// <returns>The report that was stored before, null if there was none</returns>
        ValueTask<LatencyReport?> Set(Participant participant, LatencyReport report);

        /// <returns>Returns true if there was a report</returns>
        ValueTask<bool> Remove(Participant participant);

        ValueTask<IReadOnlyDictionary<string, LatencyReport>> GetAll(string conferenceId);
    }
}
