using System;
using System.Linq;
using Strive.Admin;
using Xunit;

namespace Strive.Tests.Admin
{
    public class ActiveConferenceTrackerTests
    {
        private class FixedTime : TimeProvider
        {
            public DateTimeOffset Now { get; set; } = DateTimeOffset.FromUnixTimeSeconds(1700000000);

            public override DateTimeOffset GetUtcNow() => Now;
        }

        private readonly FixedTime _time = new();

        private ActiveConferenceTracker Create() => new(_time);

        [Fact]
        public void ParticipantsAreCountedPerConference()
        {
            var tracker = Create();
            tracker.ConferenceOpened("c1");
            tracker.ParticipantJoined("c1", "a");
            tracker.ParticipantJoined("c1", "b");
            tracker.ParticipantJoined("c2", "c");

            var snapshot = tracker.Snapshot();

            Assert.Equal(2, snapshot.Count);
            Assert.Equal(("c1", 2), (snapshot[0].ConferenceId, snapshot[0].Participants));
            Assert.Equal(_time.Now, snapshot[0].OpenedAt);
            Assert.Equal(("c2", 1), (snapshot[1].ConferenceId, snapshot[1].Participants));
            Assert.Null(snapshot[1].OpenedAt);
        }

        [Fact]
        public void JoiningTwice_CountsOnce()
        {
            var tracker = Create();
            tracker.ParticipantJoined("c1", "a");
            tracker.ParticipantJoined("c1", "a");

            Assert.Equal(1, tracker.Snapshot().Single().Participants);
        }

        [Fact]
        public void LeavingRemovesTheParticipant_AnOpenConferenceStaysListed()
        {
            var tracker = Create();
            tracker.ConferenceOpened("c1");
            tracker.ParticipantJoined("c1", "a");

            tracker.ParticipantLeft("c1", "a");
            tracker.ParticipantLeft("c1", "never-joined");
            tracker.ParticipantLeft("unknown", "a");

            var only = tracker.Snapshot().Single();
            Assert.Equal(("c1", 0), (only.ConferenceId, only.Participants));
        }

        [Fact]
        public void ClosingTheConference_RemovesIt()
        {
            var tracker = Create();
            tracker.ConferenceOpened("c1");
            tracker.ParticipantJoined("c1", "a");

            tracker.ConferenceClosed("c1");

            Assert.Empty(tracker.Snapshot());
        }

        [Fact]
        public void TheRecorderIsNotAPerson()
        {
            var tracker = Create();
            tracker.ParticipantJoined("c1", "recorder-abc");
            tracker.ParticipantJoined("c1", "a");

            Assert.Equal(1, tracker.Snapshot().Single().Participants);
        }

        [Fact]
        public void BusiestConferenceFirst()
        {
            var tracker = Create();
            tracker.ParticipantJoined("small", "a");
            foreach (var p in new[] {"1", "2", "3"}) tracker.ParticipantJoined("big", p);

            Assert.Equal(new[] {"big", "small"}, tracker.Snapshot().Select(x => x.ConferenceId));
        }
    }

    public class MetricsThinningTests
    {
        private static System.Collections.Generic.IReadOnlyList<AdminSample> Samples(int n) =>
            Enumerable.Range(0, n).Select(i => new AdminSample {Participants = i}).ToList();

        [Fact]
        public void FewPoints_AreKept()
        {
            Assert.Equal(10, MongoAdminMetricsStore.Thin(Samples(10), 400).Count);
        }

        [Fact]
        public void ManyPoints_AreSpreadEvenly_FirstPointIsKept()
        {
            var thinned = MongoAdminMetricsStore.Thin(Samples(1000), 100);

            Assert.Equal(100, thinned.Count);
            Assert.Equal(0, thinned[0].Participants);
            Assert.True(thinned.Zip(thinned.Skip(1), (a, b) => b.Participants > a.Participants).All(x => x));
            Assert.InRange(thinned[^1].Participants, 980, 999);
        }
    }
}
