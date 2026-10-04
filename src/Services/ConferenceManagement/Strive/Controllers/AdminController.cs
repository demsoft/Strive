using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Strive.Admin;

namespace Strive.Controllers
{
    /// <summary>The overview for server administrators: is the server full, what runs, who uses it.</summary>
    [ApiController]
    [Route("v1/admin")]
    [Authorize(Policy = AdminOptions.Policy)]
    public class AdminController : ControllerBase
    {
        public const int MaxHistoryHours = 7 * 24;
        private const int MaxPoints = 400;

        private readonly IAdminMetricsStore _history;
        private readonly AdminOverviewService _overview;

        public AdminController(AdminOverviewService overview, IAdminMetricsStore history)
        {
            _overview = overview;
            _history = history;
        }

        [HttpGet("overview")]
        public async Task<ActionResult<AdminOverview>> GetOverview(CancellationToken cancellationToken)
        {
            Response.Headers.CacheControl = "no-store";
            return await _overview.GetOverviewAsync(cancellationToken);
        }

        [HttpGet("history")]
        public async Task<ActionResult<object>> GetHistory([FromQuery] int hours = 24)
        {
            hours = Math.Clamp(hours, 1, MaxHistoryHours);
            var samples = await _history.GetAsync(DateTime.UtcNow.AddHours(-hours), MaxPoints);

            Response.Headers.CacheControl = "no-store";
            return new
            {
                Hours = hours,
                Samples = samples.Select(x => new
                {
                    T = DateTime.SpecifyKind(x.T, DateTimeKind.Utc), x.Participants, x.Conferences, x.MaxWorkerCpu,
                    x.AverageWorkerCpu, x.MemoryUsedPercent, x.LoadPerCore, x.Alerts,
                }),
            };
        }
    }
}
