#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Identity.API.Accounts
{
    public interface IUserRepository
    {
        Task<StriveUser?> FindByIdAsync(string id);
        Task<StriveUser?> FindByEmailAsync(string normalizedEmail);
        Task<StriveUser?> FindByLoginAsync(string provider, string key);
        Task<IReadOnlyList<StriveUser>> FindByIdsAsync(IEnumerable<string> ids);

        /// <summary>Returns false when a user with the same email exists already.</summary>
        Task<bool> TryInsertAsync(StriveUser user);

        Task UpdateAsync(StriveUser user);

        /// <summary>Returns false when that external account is linked to a user already.</summary>
        Task<bool> TryAddLoginAsync(string provider, string key, string userId);

        /// <summary>Numbers for the admin overview: totals and the sign ups of the last days (UTC dates, oldest first).</summary>
        Task<AccountStats> GetStatsAsync(int days, DateTimeOffset now);

        Task SaveTokenAsync(UserToken token);

        Task<UserToken?> PeekTokenAsync(string tokenHash, TokenPurpose purpose);

        /// <summary>Atomically takes the token: it can be used only once, and only before it expires.</summary>
        Task<UserToken?> ConsumeTokenAsync(string tokenHash, TokenPurpose purpose);

        Task DeleteTokensAsync(string userId, TokenPurpose purpose);
    }
}
