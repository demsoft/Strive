using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Identity.API.Accounts;

namespace Identity.API.Tests
{
    public class RecordingEmailSender : IEmailSender
    {
        public ConcurrentQueue<EmailMessage> Sent { get; } = new();
        public bool Fail { get; set; }

        public Task SendAsync(EmailMessage message)
        {
            if (Fail) throw new InvalidOperationException("smtp is down");
            Sent.Enqueue(message);
            return Task.CompletedTask;
        }

        public EmailMessage Last => Sent.Last();
    }

    /// <summary>The same behavior as the MongoDB repository, in memory.</summary>
    public class InMemoryUserRepository : IUserRepository
    {
        private readonly object _lock = new();
        private readonly Dictionary<string, StriveUser> _users = new();
        private readonly Dictionary<string, string> _logins = new();
        private readonly Dictionary<string, UserToken> _tokens = new();

        public Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;

        public Task<StriveUser?> FindByIdAsync(string id)
        {
            lock (_lock) return Task.FromResult(_users.GetValueOrDefault(id));
        }

        public Task<StriveUser?> FindByEmailAsync(string normalizedEmail)
        {
            lock (_lock) return Task.FromResult(_users.Values.FirstOrDefault(x => x.NormalizedEmail == normalizedEmail));
        }

        public Task<StriveUser?> FindByLoginAsync(string provider, string key)
        {
            lock (_lock)
                return Task.FromResult(_logins.TryGetValue(ExternalLogin.BuildId(provider, key), out var id)
                    ? _users.GetValueOrDefault(id)
                    : null);
        }

        public Task<IReadOnlyList<StriveUser>> FindByIdsAsync(IEnumerable<string> ids)
        {
            lock (_lock)
                return Task.FromResult<IReadOnlyList<StriveUser>>(
                    ids.Distinct().Where(_users.ContainsKey).Select(x => _users[x]).ToList());
        }

        public Task<bool> TryInsertAsync(StriveUser user)
        {
            lock (_lock)
            {
                if (_users.Values.Any(x => x.NormalizedEmail == user.NormalizedEmail)) return Task.FromResult(false);
                _users[user.Id] = user;
                return Task.FromResult(true);
            }
        }

        public Task UpdateAsync(StriveUser user)
        {
            lock (_lock) _users[user.Id] = user;
            return Task.CompletedTask;
        }

        public Task<bool> TryAddLoginAsync(string provider, string key, string userId)
        {
            lock (_lock) return Task.FromResult(_logins.TryAdd(ExternalLogin.BuildId(provider, key), userId));
        }

        public Task<AccountStats> GetStatsAsync(int days, DateTimeOffset now)
        {
            lock (_lock)
            {
                var since = now.UtcDateTime.Date.AddDays(-(days - 1));
                var signups = Enumerable.Range(0, days).Select(i => since.AddDays(i)).Select(d =>
                    new SignupsPerDay(d.ToString("yyyy-MM-dd"), _users.Values.Count(u => u.CreatedAt.UtcDateTime.Date == d))).ToList();
                return Task.FromResult(new AccountStats(_users.Count, _users.Values.Count(x => x.EmailConfirmed),
                    _users.Values.Count(x => x.PasswordHash != null), _logins.Count, signups, now));
            }
        }

        public Task SaveTokenAsync(UserToken token)
        {
            lock (_lock) _tokens[token.Id] = token;
            return Task.CompletedTask;
        }

        public Task<UserToken?> PeekTokenAsync(string tokenHash, TokenPurpose purpose)
        {
            lock (_lock)
                return Task.FromResult(_tokens.TryGetValue(tokenHash, out var t) && t.Purpose == purpose &&
                                       t.ExpiresAt > UtcNow()
                    ? t
                    : null);
        }

        public Task<UserToken?> ConsumeTokenAsync(string tokenHash, TokenPurpose purpose)
        {
            lock (_lock)
            {
                if (!_tokens.TryGetValue(tokenHash, out var t) || t.Purpose != purpose || t.ExpiresAt <= UtcNow())
                    return Task.FromResult<UserToken?>(null);
                _tokens.Remove(tokenHash);
                return Task.FromResult<UserToken?>(t);
            }
        }

        public Task DeleteTokensAsync(string userId, TokenPurpose purpose)
        {
            lock (_lock)
                foreach (var key in _tokens.Where(x => x.Value.UserId == userId && x.Value.Purpose == purpose)
                             .Select(x => x.Key).ToList())
                    _tokens.Remove(key);
            return Task.CompletedTask;
        }
    }
}
