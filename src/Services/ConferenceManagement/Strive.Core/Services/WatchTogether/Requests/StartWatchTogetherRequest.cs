using MediatR;

namespace Strive.Core.Services.WatchTogether.Requests
{
    public record StartWatchTogetherRequest(Participant Participant, string Url) : IRequest;
}
