#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Identity.API.Quickstart;

namespace Identity.API.Accounts
{
    /// <summary>Looks up the display names of users by their subject id (the conference service shows them).</summary>
    public interface IUserDirectory
    {
        Task<IReadOnlyDictionary<string, string?>> GetDisplayNamesAsync(IEnumerable<string> ids);
    }

    public class DemoUserDirectory : IUserDirectory
    {
        private readonly IUserProvider _users;

        public DemoUserDirectory(IUserProvider users)
        {
            _users = users;
        }

        public Task<IReadOnlyDictionary<string, string?>> GetDisplayNamesAsync(IEnumerable<string> ids)
        {
            IReadOnlyDictionary<string, string?> result = ids.Distinct().ToDictionary(id => id, SafeName);
            return Task.FromResult(result);
        }

        private string? SafeName(string id)
        {
            try
            {
                return _users.IdToUsername(id);
            }
            catch (System.FormatException)
            {
                return null;
            }
        }
    }

    public class AccountsUserDirectory : IUserDirectory
    {
        private readonly IUserRepository _users;

        public AccountsUserDirectory(IUserRepository users)
        {
            _users = users;
        }

        public async Task<IReadOnlyDictionary<string, string?>> GetDisplayNamesAsync(IEnumerable<string> ids)
        {
            var list = ids.Distinct().ToList();
            var found = (await _users.FindByIdsAsync(list)).ToDictionary(x => x.Id, x => x.DisplayName);
            return list.ToDictionary(id => id, id => found.TryGetValue(id, out var name) ? name : null);
        }
    }
}
