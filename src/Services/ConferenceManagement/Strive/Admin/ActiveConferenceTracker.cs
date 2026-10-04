using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Strive.Core.Services.Recording;

namespace Strive.Admin
{
    /// <summary>
    ///     Who is in which conference right now, for the admin overview. Kept in memory from the notifications of the
    ///     conferences: after a restart it fills again as the participants reconnect.
    /// </summary>
    public class ActiveConferenceTracker
    {
        private readonly ConcurrentDictionary<string, DateTimeOffset> _opened = new();

        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, DateTimeOffset>> _participants =
            new();

        private readonly TimeProvider _timeProvider;

        public ActiveConferenceTracker(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
        }

        public void ConferenceClosed(string conferenceId)
        {
            _opened.TryRemove(conferenceId, out _);
            _participants.TryRemove(conferenceId, out _);
        }

        public void ConferenceOpened(string conferenceId)
        {
            _opened[conferenceId] = _timeProvider.GetUtcNow();
        }

        public void ParticipantJoined(string conferenceId, string participantId)
        {
            // the recorder is not a person
            if (RecorderParticipants.IsRecorder(participantId)) return;

            _participants.GetOrAdd(conferenceId, _ => new ConcurrentDictionary<string, DateTimeOffset>())[
                participantId] = _timeProvider.GetUtcNow();
        }

        public void ParticipantLeft(string conferenceId, string participantId)
        {
            if (_participants.TryGetValue(conferenceId, out var inConference))
                inConference.TryRemove(participantId, out _);
        }

        /// <returns>Conference id, number of participants and when it was opened</returns>
        public IReadOnlyList<(string ConferenceId, int Participants, DateTimeOffset? OpenedAt)> Snapshot()
        {
            var ids = _opened.Keys.Union(_participants.Keys).Distinct();
            return ids.Select(id => (id, _participants.TryGetValue(id, out var p) ? p.Count : 0,
                    _opened.TryGetValue(id, out var opened) ? opened : (DateTimeOffset?) null))
                .OrderByDescending(x => x.Item2).ToList();
        }
    }
}

