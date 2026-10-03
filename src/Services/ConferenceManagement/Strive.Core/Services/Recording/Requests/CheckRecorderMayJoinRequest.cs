using MediatR;

namespace Strive.Core.Services.Recording.Requests
{
    /// <summary>
    ///     Is the recording with this id still running in this conference? A recorder token stays valid for hours, but
    ///     must not let anybody join once the recording is over.
    /// </summary>
    public record CheckRecorderMayJoinRequest(Participant Participant, string RecordingId) : IRequest<bool>;
}
