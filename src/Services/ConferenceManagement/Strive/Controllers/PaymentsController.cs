using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Strive.Core.Services.Payments;
using Strive.Core.Services.Payments.Gateways;
using Strive.Core.Services.Payments.Requests;
using Strive.Core.Services.Payments.Sessions;
using Strive.Extensions;
using Strive.Models.Response;

namespace Strive.Controllers
{
    /// <summary>The state of a payment, and the notifications of the payment provider.</summary>
    [ApiController]
    public class PaymentsController : Controller
    {
        private readonly IMediator _mediator;
        private readonly IOrderRepo _orders;
        private readonly IPaymentProvider _provider;
        private readonly ILogger<PaymentsController> _logger;

        public PaymentsController(IMediator mediator, IOrderRepo orders, IPaymentProvider provider,
            ILogger<PaymentsController> logger)
        {
            _mediator = mediator;
            _orders = orders;
            _provider = provider;
            _logger = logger;
        }

        // GET v1/payments/{reference}
        /// <summary>
        ///     The page the person comes back to after paying asks this. If the payment is not settled yet, the provider is
        ///     asked right now (the notification may be a moment late), so the person is not left waiting.
        /// </summary>
        [HttpGet("v1/payments/{reference}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PaymentStatusDto>> GetStatus(string reference)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Forbid();

            // somebody else's payment does not exist for you
            var existing = await _orders.FindByReference(reference);
            if (existing == null || existing.BuyerUserId != userId) return PaymentsError.OrderNotFound.ToActionResult();

            var result = await _mediator.Send(new SettleOrderRequest(reference));
            if (!result.Success) return result.ToActionResult();

            var order = result.Response!;
            var paid = order.Status == OrderStatus.Paid;
            Response.Headers.CacheControl = "no-store";
            var isPass = order.Kind == OrderKind.Pass;
            return new PaymentStatusDto(order.Reference, order.Status.ToString().ToLowerInvariant(), order.SessionId,
                paid, paid && !isPass ? order.ConferenceId : null, order.FailureReason,
                isPass ? "pass" : "ticket", order.PassCreatorId);
        }

        // POST webhooks/paystack
        /// <summary>
        ///     The notification of Paystack. It is trusted for nothing but the reference: the signature proves where it
        ///     comes from, and the payment itself is checked with Paystack before anybody gets a ticket.
        /// </summary>
        [HttpPost("webhooks/paystack")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult> PaystackWebhook()
        {
            using var buffer = new MemoryStream();
            await Request.Body.CopyToAsync(buffer, HttpContext.RequestAborted);
            var raw = buffer.ToArray();

            if (!_provider.IsValidWebhookSignature(raw, Request.Headers["x-paystack-signature"].ToString()))
            {
                _logger.LogWarning("A notification with a wrong signature was refused");
                return Unauthorized();
            }

            string? eventName = null;
            string? reference = null;
            try
            {
                using var document = JsonDocument.Parse(raw);
                eventName = document.RootElement.TryGetProperty("event", out var e) ? e.GetString() : null;
                if (document.RootElement.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("reference", out var r))
                    reference = r.GetString();
            }
            catch (JsonException)
            {
                // a signed message that is not JSON: nothing to do
            }

            if (eventName == "charge.success" && !string.IsNullOrEmpty(reference))
            {
                var result = await _mediator.Send(new SettleOrderRequest(reference));
                // Paystack repeats a notification that is not answered with 200: an unknown reference is not worth it
                if (!result.Success && result.Error!.Code != nameof(Strive.Core.Services.ServiceErrorCode.Payments_OrderNotFound))
                {
                    _logger.LogWarning("Order {reference} could not be settled: {code}", reference, result.Error.Code);
                    return StatusCode(StatusCodes.Status503ServiceUnavailable);
                }
            }

            return Ok();
        }
    }
}
