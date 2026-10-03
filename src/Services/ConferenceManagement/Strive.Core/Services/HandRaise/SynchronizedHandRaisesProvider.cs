using System.Threading.Tasks;
using Strive.Core.Services.HandRaise.Gateways;
using Strive.Core.Services.Synchronization;

namespace Strive.Core.Services.HandRaise
{
    public class SynchronizedHandRaisesProvider : SynchronizedObjectProviderForAll<SynchronizedHandRaises>
    {
        private readonly IHandRaiseRepository _repository;

        public SynchronizedHandRaisesProvider(IHandRaiseRepository repository)
        {
            _repository = repository;
        }

        public override string Id { get; } = SynchronizedHandRaises.SyncObjId.Id;

        protected override async ValueTask<SynchronizedHandRaises> InternalFetchValue(string conferenceId)
        {
            return new SynchronizedHandRaises(await _repository.GetAll(conferenceId));
        }
    }
}
