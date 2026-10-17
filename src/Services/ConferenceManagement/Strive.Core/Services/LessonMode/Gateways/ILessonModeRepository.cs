using System.Threading.Tasks;
using Strive.Core.Interfaces.Gateways;

namespace Strive.Core.Services.LessonMode.Gateways
{
    public interface ILessonModeRepository : IStateRepository
    {
        ValueTask<bool> IsActive(string conferenceId);

        /// <returns>Returns true if the state changed</returns>
        ValueTask<bool> Set(string conferenceId, bool active);
    }
}
