using System.Threading.Tasks;
using Strive.Core.Interfaces.Gateways;

namespace Strive.Core.Services.WatchTogether.Gateways
{
    public interface IWatchTogetherRepository : IStateRepository
    {
        ValueTask<WatchTogetherSession?> Get(string conferenceId);

        ValueTask Set(string conferenceId, WatchTogetherSession session);

        /// <returns>Returns true if there was a video</returns>
        ValueTask<bool> Clear(string conferenceId);
    }
}
