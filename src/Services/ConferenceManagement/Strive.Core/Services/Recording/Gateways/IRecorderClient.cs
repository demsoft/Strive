using System.Threading;
using System.Threading.Tasks;

namespace Strive.Core.Services.Recording.Gateways
{
    /// <summary>
    ///     Talks to the recorder service that joins a conference as a hidden participant and records it
    /// </summary>
    public interface IRecorderClient
    {
        Task Start(RecorderStartCommand command, CancellationToken cancellationToken = default);

        Task Stop(string recordingId, CancellationToken cancellationToken = default);
    }

    /// <param name="RecordingId">The recording this recorder works for</param>
    /// <param name="ConferenceId">The conference to record</param>
    /// <param name="JoinToken">A token that lets the recorder join the conference as a hidden participant</param>
    /// <param name="StorageKey">Where the recorder must store the file</param>
    /// <param name="MaxDurationMinutes">The recorder stops by itself after this time</param>
    public record RecorderStartCommand(string RecordingId, string ConferenceId, string JoinToken, string StorageKey,
        int MaxDurationMinutes);
}
