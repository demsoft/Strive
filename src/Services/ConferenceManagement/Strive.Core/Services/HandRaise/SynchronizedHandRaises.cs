using System;
using System.Collections.Generic;
using Strive.Core.Services.Synchronization;

namespace Strive.Core.Services.HandRaise
{
    /// <param name="Raised">The participants with a raised hand (participant id) and when they raised it. Clients
    ///     order them by time, the first participant that raised the hand is first in line.</param>
    public record SynchronizedHandRaises(IReadOnlyDictionary<string, DateTimeOffset> Raised)
    {
        public static readonly SynchronizedObjectId SyncObjId = new(SynchronizedObjectIds.HAND_RAISES);
    }
}
