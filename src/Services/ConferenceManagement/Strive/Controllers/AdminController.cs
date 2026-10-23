using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Strive.Admin;
using Strive.Core.Services.Payments;
using Strive.Core.Services.Payments.Gateways;
using Strive.Core.Services.Payments.Sessions;
using Strive.Models.Response;

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
        private readonly IOrderRepo _orders;
        private readonly ISessionRepo _sessions;
        private readonly ICreatorRepo _creators;
        private readonly IPassSubscriptionRepo _subscriptions;
        private readonly PaymentsOptions _payments;

        public AdminController(AdminOverviewService overview, IAdminMetricsStore history, IOrderRepo orders,
            ISessionRepo sessions, ICreatorRepo creators, IPassSubscriptionRepo subscriptions,
            IOptions<PaymentsOptions> payments)
        {
            _overview = overview;
            _history = history;
            _orders = orders;
            _sessions = sessions;
            _creators = creators;
            _subscriptions = subscriptions;
            _payments = payments.Value;
        }

        /// <summary>Money: what was sold and paid, and what needs a person (refunds that failed, renewals that fail)</summary>
        [HttpGet("payments")]
        public async Task<ActionResult<AdminPaymentsDto>> GetPayments()
        {
            var now = DateTimeOffset.UtcNow;
            var monthOrders = await _orders.FindPaidSince(now.AddDays(-30), 20000);
            var recent = await _orders.FindRecent(30);
            var refundProblems = await _orders.FindWithRefundFailure(50);
            var subscriptionProblems = await _subscriptions.FindWithProblems(now.AddDays(-30), 50);

            var all = recent.Concat(refundProblems).ToList();
            var names = await _creators.FindNames(all.Select(x => PaymentsOverview.CreatorOf(x))
                .Concat(subscriptionProblems.Select(x => (string?) x.CreatorId)).Where(x => x != null).Select(x => x!));
            var titles = new Dictionary<string, string>();
            foreach (var id in all.Where(x => x.Kind == OrderKind.Ticket).Select(x => x.SessionId).Distinct())
            {
                var session = await _sessions.FindById(id);
                if (session != null) titles[id] = session.Title;
            }

            Response.Headers.CacheControl = "no-store";
            return new AdminPaymentsDto(_payments.Currency, _payments.Enabled, _payments.AutoRenewEnabled,
                await _creators.Count(), await _subscriptions.CountActive(),
                PaymentsOverview.Period(monthOrders, now, 7), PaymentsOverview.Period(monthOrders, now, 30),
                recent.Select(x => PaymentsOverview.Row(x, names, titles)).ToList(),
                refundProblems.Select(x => PaymentsOverview.Refund(x, names, titles)).ToList(),
                subscriptionProblems.Select(x => PaymentsOverview.Subscription(x, names)).ToList());
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
