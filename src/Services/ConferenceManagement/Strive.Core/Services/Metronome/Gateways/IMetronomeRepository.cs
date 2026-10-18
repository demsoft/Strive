using System.Threading.Tasks;
using Strive.Core.Interfaces.Gateways;

namespace Strive.Core.Services.Metronome.Gateways
{
    public interface IMetronomeRepository : IStateRepository
    {
        ValueTask<MetronomeSession?> Get(string conferenceId);

        ValueTask Set(string conferenceId, MetronomeSession session);

        /// <returns>Returns true if the metronome was running</returns>
        ValueTask<bool> Clear(string conferenceId);
    }
}
