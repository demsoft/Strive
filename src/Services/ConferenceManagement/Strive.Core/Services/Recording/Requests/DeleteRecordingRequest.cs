using MediatR;
using Strive.Core.Interfaces;

namespace Strive.Core.Services.Recording.Requests
{
    public record DeleteRecordingRequest(string RecordingId) : IRequest<SuccessOrError<Unit>>;
}
