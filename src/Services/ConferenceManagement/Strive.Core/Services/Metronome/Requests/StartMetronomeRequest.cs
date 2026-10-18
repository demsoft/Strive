using MediatR;

namespace Strive.Core.Services.Metronome.Requests
{
    /// <summary>Starts the click, or changes its tempo (the count starts again)</summary>
    public record StartMetronomeRequest(Participant Participant, int Bpm, int BeatsPerBar) : IRequest;
}
