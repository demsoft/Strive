using MediatR;

namespace Strive.Core.Services.Metronome.Requests
{
    public record StopMetronomeRequest(string ConferenceId) : IRequest;
}
