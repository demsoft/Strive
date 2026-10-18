using System;

namespace Strive.Core.Services.Metronome
{
    /// <summary>
    ///     A click that every participant hears at the same time. It is described by the tempo and the moment of the first
    ///     beat (server time), so that every client can compute when every beat is, without receiving a message per beat.
    /// </summary>
    /// <param name="Bpm">Beats per minute</param>
    /// <param name="BeatsPerBar">Beats in a bar, the first of them gets the accent</param>
    /// <param name="StartedBy">Participant id of the participant that started it</param>
    /// <param name="FirstBeatAt">The moment of the first beat (server time), a moment after the start, so that everybody
    /// has received the message before the first beat is due</param>
    public record MetronomeSession(int Bpm, int BeatsPerBar, string StartedBy, DateTimeOffset FirstBeatAt)
    {
        public const int MinBpm = 30;
        public const int MaxBpm = 300;
        public const int MinBeatsPerBar = 1;
        public const int MaxBeatsPerBar = 12;

        /// <summary>How long after the start of the first beat is, for the message to reach everybody</summary>
        public static readonly TimeSpan LeadTime = TimeSpan.FromMilliseconds(1500);

        public static bool IsValid(int bpm, int beatsPerBar)
        {
            return bpm is >= MinBpm and <= MaxBpm && beatsPerBar is >= MinBeatsPerBar and <= MaxBeatsPerBar;
        }
    }
}
