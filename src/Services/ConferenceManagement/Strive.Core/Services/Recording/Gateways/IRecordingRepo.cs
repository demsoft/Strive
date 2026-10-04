using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Strive.Core.Services.Recording.Gateways
{
    public interface IRecordingRepo
    {
        Task<bool> TryCreate(ConferenceRecording recording);

        Task<ConferenceRecording?> FindById(string recordingId);

        Task<ConferenceRecording?> FindByShareToken(string shareToken);

        /// <summary>
        ///     The recording of the conference that is not finished (starting, recording or finalizing)
        /// </summary>
        Task<ConferenceRecording?> FindActiveOfConference(string conferenceId);

        Task<IReadOnlyList<ConferenceRecording>> FindOfConference(string conferenceId);

        /// <summary>The recordings that a participant started, newest first (at most the given number)</summary>
        Task<IReadOnlyList<ConferenceRecording>> FindStartedBy(string participantId, int limit);

        Task<IReadOnlyList<ConferenceRecording>> FindExpired(DateTimeOffset now);

        /// <summary>
        ///     Recordings that are not finished and started before the date (the recorder may have died)
        /// </summary>
        Task<IReadOnlyList<ConferenceRecording>> FindUnfinishedStartedBefore(DateTimeOffset date);

        Task Update(ConferenceRecording recording);

        Task Delete(string recordingId);
    }
}
