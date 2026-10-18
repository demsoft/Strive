using Strive.Core.Dto;
using Strive.Core.Errors;

namespace Strive.Core.Services.Metronome
{
    public class MetronomeError : ErrorsProvider<ServiceErrorCode>
    {
        public static Error InvalidTempo =>
            BadRequest(
                $"The tempo must be between {MetronomeSession.MinBpm} and {MetronomeSession.MaxBpm} beats per minute, with {MetronomeSession.MinBeatsPerBar} to {MetronomeSession.MaxBeatsPerBar} beats per bar.",
                ServiceErrorCode.Metronome_InvalidTempo);
    }
}
