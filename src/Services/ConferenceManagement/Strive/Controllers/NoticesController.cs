using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Strive.Core.Services.Social;
using Strive.Extensions;
using Strive.Models.Response;

namespace Strive.Controllers
{
    /// <summary>
    ///     The links in the mails to followers. No sign in: the secret in the link is the proof that the person has the
    ///     mail. They are POSTs so that a program that opens the links of a mail (a spam filter) changes nothing.
    /// </summary>
    [ApiController]
    [AllowAnonymous]
    public class NoticesController : Controller
    {
        private readonly IMediator _mediator;

        public NoticesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // POST v1/notices/{token}/confirm
        [HttpPost("v1/notices/{token}/confirm")]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<NoticeAnswerDto>> Confirm(string token)
        {
            var result = await _mediator.Send(new ConfirmEmailNoticesRequest(token));
            if (!result.Success) return result.ToActionResult();

            Response.Headers.CacheControl = "no-store";
            return new NoticeAnswerDto(result.Response!.CreatorName, result.Response.Status.ToString().ToLowerInvariant());
        }

        // POST v1/notices/{token}/stop
        [HttpPost("v1/notices/{token}/stop")]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<NoticeAnswerDto>> Stop(string token)
        {
            var result = await _mediator.Send(new StopEmailNoticesByTokenRequest(token));
            if (!result.Success) return result.ToActionResult();

            Response.Headers.CacheControl = "no-store";
            return new NoticeAnswerDto(result.Response!.CreatorName, result.Response.Status.ToString().ToLowerInvariant());
        }
    }
}
