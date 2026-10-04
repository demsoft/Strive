#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Identity.API.Accounts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Identity.API.Controllers
{
    /// <summary>The account numbers for the admin overview of the API. Not for browsers: it needs the shared api key.</summary>
    [Route("api/v1/admin")]
    public class AdminController : Controller
    {
        public const int Days = 14;

        private readonly AccountsOptions _options;
        private readonly IUserRepository? _users;

        public AdminController(IOptions<AccountsOptions> options, IUserRepository? users = null)
        {
            _options = options.Value;
            _users = users;
        }

        [HttpGet("stats")]
        public async Task<IActionResult> Stats()
        {
            var key = _options.AdminApiKey;
            if (string.IsNullOrEmpty(key) || !Request.Headers.TryGetValue("X-Api-Key", out var provided) ||
                !FixedTimeEquals(provided.ToString(), key))
                return Unauthorized();

            // demo mode has no accounts
            if (!_options.IsAccountsMode || _users == null) return Ok(new {Available = false});

            var stats = await _users.GetStatsAsync(Days, DateTimeOffset.UtcNow);
            return Ok(new
            {
                Available = true, stats.TotalUsers, stats.ConfirmedUsers, stats.WithPassword, stats.WithGoogle,
                stats.Signups, stats.GeneratedAt,
            });
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            var left = SHA256.HashData(Encoding.UTF8.GetBytes(a));
            var right = SHA256.HashData(Encoding.UTF8.GetBytes(b));
            return CryptographicOperations.FixedTimeEquals(left, right);
        }
    }
}
