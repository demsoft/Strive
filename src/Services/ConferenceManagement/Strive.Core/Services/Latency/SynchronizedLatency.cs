using System.Collections.Generic;
using Strive.Core.Services.Synchronization;

namespace Strive.Core.Services.Latency
{
    /// <param name="Reports">The measurements of the participants that report them (musicians), by participant id</param>
    public record SynchronizedLatency(IReadOnlyDictionary<string, LatencyReport> Reports)
    {
        public static readonly SynchronizedObjectId SyncObjId = new(SynchronizedObjectIds.LATENCY);
    }
}
