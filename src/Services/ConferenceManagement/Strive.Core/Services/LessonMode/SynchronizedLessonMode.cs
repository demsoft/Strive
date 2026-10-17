using Strive.Core.Services.Synchronization;

namespace Strive.Core.Services.LessonMode
{
    /// <param name="Active">True while only the moderators can be heard (everybody else is muted)</param>
    public record SynchronizedLessonMode(bool Active)
    {
        public static readonly SynchronizedObjectId SyncObjId = new(SynchronizedObjectIds.LESSON_MODE);
    }
}
