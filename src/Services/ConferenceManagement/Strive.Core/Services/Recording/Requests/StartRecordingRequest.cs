using MediatR;
using Strive.Core.Interfaces;

namespace Strive.Core.Services.Recording.Requests
{
    /// <param name="Participant">The moderator that starts the recording</param>
    public record StartRecordingRequest(Participant Participant) : IRequest<SuccessOrError<Unit>>;
}
