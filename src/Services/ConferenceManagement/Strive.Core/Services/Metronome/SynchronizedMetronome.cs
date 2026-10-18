using Strive.Core.Services.Synchronization;

namespace Strive.Core.Services.Metronome
{
    /// <param name="Session">The click that is running, null if there is none</param>
    public record SynchronizedMetronome(MetronomeSession? Session)
    {
        public static readonly SynchronizedObjectId SyncObjId = new(SynchronizedObjectIds.METRONOME);
    }
}
