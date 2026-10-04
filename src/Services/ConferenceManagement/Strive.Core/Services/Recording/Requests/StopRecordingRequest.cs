using MediatR;
using Strive.Core.Interfaces;

namespace Strive.Core.Services.Recording.Requests
{
    public record StopRecordingRequest(string ConferenceId) : IRequest<SuccessOrError<Unit>>;
}
