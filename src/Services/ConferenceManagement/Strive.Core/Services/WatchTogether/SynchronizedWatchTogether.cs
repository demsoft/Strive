using Strive.Core.Services.Synchronization;

namespace Strive.Core.Services.WatchTogether
{
    /// <param name="Session">The video that is watched, null if there is none</param>
    public record SynchronizedWatchTogether(WatchTogetherSession? Session)
    {
        public static readonly SynchronizedObjectId SyncObjId = new(SynchronizedObjectIds.WATCH_TOGETHER);
    }
}
