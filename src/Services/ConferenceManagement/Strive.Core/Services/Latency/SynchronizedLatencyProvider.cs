using System.Threading.Tasks;
using Strive.Core.Services.Latency.Gateways;
using Strive.Core.Services.Synchronization;

namespace Strive.Core.Services.Latency
{
    public class SynchronizedLatencyProvider : SynchronizedObjectProviderForAll<SynchronizedLatency>
    {
        private readonly ILatencyRepository _repository;

        public SynchronizedLatencyProvider(ILatencyRepository repository)
        {
            _repository = repository;
        }

        public override string Id { get; } = SynchronizedLatency.SyncObjId.Id;

        protected override async ValueTask<SynchronizedLatency> InternalFetchValue(string conferenceId)
        {
            return new SynchronizedLatency(await _repository.GetAll(conferenceId));
        }
    }
}
