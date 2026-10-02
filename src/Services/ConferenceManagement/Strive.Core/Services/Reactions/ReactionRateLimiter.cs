using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Strive.Core.Services.Reactions
{
    /// <summary>
    ///     Sliding window: at most <see cref="MaxReactions" /> reactions per <see cref="Window" />
    /// </summary>
    public class ReactionRateLimiter : IReactionRateLimiter
    {
        public const int MaxReactions = 5;
        public static readonly TimeSpan Window = TimeSpan.FromSeconds(3);

        private readonly TimeProvider _timeProvider;
        private readonly ConcurrentDictionary<Participant, Queue<DateTimeOffset>> _sent = new();

        public ReactionRateLimiter(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
        }

        public bool TryAcquire(Participant participant)
        {
            var now = _timeProvider.GetUtcNow();
            var queue = _sent.GetOrAdd(participant, _ => new Queue<DateTimeOffset>());

            lock (queue)
            {
                while (queue.Count > 0 && now - queue.Peek() >= Window) queue.Dequeue();

                if (queue.Count >= MaxReactions) return false;

                queue.Enqueue(now);
                return true;
            }
        }

        public void Remove(Participant participant)
        {
            _sent.TryRemove(participant, out _);
        }
    }
}
