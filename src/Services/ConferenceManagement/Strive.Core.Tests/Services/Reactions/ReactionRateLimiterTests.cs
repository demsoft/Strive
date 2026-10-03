using System;
using Strive.Core.Services;
using Strive.Core.Services.Reactions;
using Xunit;

namespace Strive.Core.Tests.Services.Reactions
{
    public class ReactionRateLimiterTests
    {
        private readonly Participant _participant = new("123", "participant");
        private readonly TestTimeProvider _time = new();

        private class TestTimeProvider : TimeProvider
        {
            public DateTimeOffset Now { get; set; } = DateTimeOffset.FromUnixTimeSeconds(1700000000);

            public override DateTimeOffset GetUtcNow()
            {
                return Now;
            }
        }

        private ReactionRateLimiter Create()
        {
            return new ReactionRateLimiter(_time);
        }

        [Fact]
        public void TryAcquire_BelowLimit_AllowAll()
        {
            var limiter = Create();

            for (var i = 0; i < ReactionRateLimiter.MaxReactions; i++)
                Assert.True(limiter.TryAcquire(_participant));
        }

        [Fact]
        public void TryAcquire_LimitExceeded_Deny()
        {
            var limiter = Create();
            for (var i = 0; i < ReactionRateLimiter.MaxReactions; i++) limiter.TryAcquire(_participant);

            Assert.False(limiter.TryAcquire(_participant));
        }

        [Fact]
        public void TryAcquire_WindowPassed_AllowAgain()
        {
            var limiter = Create();
            for (var i = 0; i < ReactionRateLimiter.MaxReactions; i++) limiter.TryAcquire(_participant);

            _time.Now += ReactionRateLimiter.Window;

            Assert.True(limiter.TryAcquire(_participant));
        }

        [Fact]
        public void TryAcquire_OtherParticipantHitLimit_NotAffected()
        {
            var limiter = Create();
            for (var i = 0; i < ReactionRateLimiter.MaxReactions; i++) limiter.TryAcquire(_participant);

            Assert.True(limiter.TryAcquire(new Participant("123", "other")));
        }

        [Fact]
        public void Remove_LimitExceeded_AllowAgain()
        {
            var limiter = Create();
            for (var i = 0; i < ReactionRateLimiter.MaxReactions; i++) limiter.TryAcquire(_participant);

            limiter.Remove(_participant);

            Assert.True(limiter.TryAcquire(_participant));
        }
    }
}
