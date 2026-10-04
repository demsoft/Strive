using System.Threading.Tasks;
using Strive.Core.Services.Synchronization;
using Strive.Core.Services.WatchTogether.Gateways;

namespace Strive.Core.Services.WatchTogether
{
    public class SynchronizedWatchTogetherProvider : SynchronizedObjectProviderForAll<SynchronizedWatchTogether>
    {
        private readonly IWatchTogetherRepository _repository;

        public SynchronizedWatchTogetherProvider(IWatchTogetherRepository repository)
        {
            _repository = repository;
        }

        public override string Id { get; } = SynchronizedWatchTogether.SyncObjId.Id;

        protected override async ValueTask<SynchronizedWatchTogether> InternalFetchValue(string conferenceId)
        {
            return new SynchronizedWatchTogether(await _repository.Get(conferenceId));
        }
    }
}
