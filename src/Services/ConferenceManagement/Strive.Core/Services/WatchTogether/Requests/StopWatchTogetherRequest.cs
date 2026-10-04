using MediatR;

namespace Strive.Core.Services.WatchTogether.Requests
{
    public record StopWatchTogetherRequest(string ConferenceId) : IRequest;
}
