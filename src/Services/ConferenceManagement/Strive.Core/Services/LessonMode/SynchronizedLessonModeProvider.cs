using System.Threading.Tasks;
using Strive.Core.Services.LessonMode.Gateways;
using Strive.Core.Services.Synchronization;

namespace Strive.Core.Services.LessonMode
{
    public class SynchronizedLessonModeProvider : SynchronizedObjectProviderForAll<SynchronizedLessonMode>
    {
        private readonly ILessonModeRepository _repository;

        public SynchronizedLessonModeProvider(ILessonModeRepository repository)
        {
            _repository = repository;
        }

        public override string Id { get; } = SynchronizedLessonMode.SyncObjId.Id;

        protected override async ValueTask<SynchronizedLessonMode> InternalFetchValue(string conferenceId)
        {
            return new SynchronizedLessonMode(await _repository.IsActive(conferenceId));
        }
    }
}
