using System.Threading.Tasks;
using Strive.Core.Services.Recording.Gateways;
using Strive.Core.Services.Synchronization;

namespace Strive.Core.Services.Recording
{
    public class SynchronizedRecordingProvider : SynchronizedObjectProviderForAll<SynchronizedRecording>
    {
        private readonly IRecordingRepo _repository;

        public SynchronizedRecordingProvider(IRecordingRepo repository)
        {
            _repository = repository;
        }

        public override string Id => SynchronizedObjectIds.RECORDING;

        protected override async ValueTask<SynchronizedRecording> InternalFetchValue(string conferenceId)
        {
            var active = await _repository.FindActiveOfConference(conferenceId);
            return new SynchronizedRecording(active == null
                ? null
                : new SynchronizedRecording.ActiveRecording(active.RecordingId, active.Status, active.StartedAt,
                    active.StartedBy));
        }
    }
}
