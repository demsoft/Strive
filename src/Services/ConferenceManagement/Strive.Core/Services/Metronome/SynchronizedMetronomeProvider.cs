using System.Threading.Tasks;
using Strive.Core.Services.Metronome.Gateways;
using Strive.Core.Services.Synchronization;

namespace Strive.Core.Services.Metronome
{
    public class SynchronizedMetronomeProvider : SynchronizedObjectProviderForAll<SynchronizedMetronome>
    {
        private readonly IMetronomeRepository _repository;

        public SynchronizedMetronomeProvider(IMetronomeRepository repository)
        {
            _repository = repository;
        }

        public override string Id { get; } = SynchronizedMetronome.SyncObjId.Id;

        protected override async ValueTask<SynchronizedMetronome> InternalFetchValue(string conferenceId)
        {
            return new SynchronizedMetronome(await _repository.Get(conferenceId));
        }
    }
}
