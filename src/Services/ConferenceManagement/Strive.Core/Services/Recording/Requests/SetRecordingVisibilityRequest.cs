using MediatR;
using Strive.Core.Interfaces;

namespace Strive.Core.Services.Recording.Requests
{
    public record SetRecordingVisibilityRequest(string RecordingId, RecordingVisibility Visibility) :
        IRequest<SuccessOrError<Unit>>;
}
