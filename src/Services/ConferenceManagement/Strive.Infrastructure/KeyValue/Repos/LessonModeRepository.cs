using System.Threading.Tasks;
using Strive.Core.Services.LessonMode.Gateways;
using Strive.Infrastructure.KeyValue.Abstractions;
using Strive.Infrastructure.KeyValue.Extensions;

namespace Strive.Infrastructure.KeyValue.Repos
{
    public class LessonModeRepository : ILessonModeRepository, IKeyValueRepo
    {
        private const string PROPERTY_KEY = "lessonMode";

        private readonly IKeyValueDatabase _database;

        public LessonModeRepository(IKeyValueDatabase database)
        {
            _database = database;
        }

        public async ValueTask<bool> IsActive(string conferenceId)
        {
            return await _database.GetAsync<bool?>(GetKey(conferenceId)) == true;
        }

        public async ValueTask<bool> Set(string conferenceId, bool active)
        {
            var before = await IsActive(conferenceId);
            if (before == active) return false;

            if (active) await _database.SetAsync(GetKey(conferenceId), true);
            else await _database.KeyDeleteAsync(GetKey(conferenceId));
            return true;
        }

        public async ValueTask RemoveAllDataOfConference(string conferenceId)
        {
            await _database.KeyDeleteAsync(GetKey(conferenceId));
        }

        private static string GetKey(string conferenceId)
        {
            return DatabaseKeyBuilder.ForProperty(PROPERTY_KEY).ForConference(conferenceId).ToString();
        }
    }
}
