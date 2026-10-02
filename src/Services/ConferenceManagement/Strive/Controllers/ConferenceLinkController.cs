using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Strive.Core;
using Strive.Core.Dto;
using Strive.Core.Errors;
using Strive.Core.Interfaces.Gateways;
using Strive.Core.Interfaces.Gateways.Repositories;
using Strive.Core.Specifications;
using Strive.Models.Request;
using Strive.Models.Response;
using Strive.Presenters;
using SpeciVacation;

namespace Strive.Controllers
{
    [Route("v1/conference-link")]
    [ApiController]
    public class ConferenceLinkController : Controller
    {
        // GET v1/conference-links
        [HttpGet]
        [Authorize]
        public async Task<ActionResult<IReadOnlyList<ConferenceLinkDto>>> GetConferenceLinks(
            [FromServices] IConferenceLinkPresenter presenter)
        {
            var userId = User.Claims.First(x => x.Type == ClaimTypes.NameIdentifier).Value;
            var result = await presenter.GetConferenceLinks(userId);

            return Ok(result);
        }

        // DELETE v1/conference-links/{conferenceId}
        [HttpDelete("{conferenceId}")]
        [Authorize]
        public async Task<ActionResult> DeleteConferenceLink(string conferenceId,
            [FromServices] IConferenceLinkRepo repo)
        {
            var userId = User.Claims.First(x => x.Type == ClaimTypes.NameIdentifier).Value;
            var conferenceLink =
                (await repo.FindAsync(
                    new ConferenceLinkByParticipant(userId).And(new ConferenceLinkByConference(conferenceId))))
                .FirstOrDefault();

            if (conferenceLink == null)
                return NotFound(new Error(ErrorType.NotFound.ToString(), "The conference link was not found",
                    "ConferenceLink_NotFound"));

            await repo.DeleteAsync(conferenceLink);
            return Ok();
        }

        // PATCH v1/conference-links/{conferenceId}
        [HttpPatch("{conferenceId}")]
        [Authorize]
        public async Task<ActionResult> PatchConferenceLink(string conferenceId,
            JsonPatchDocument<ChangeConferenceLinkStarDto> patch, [FromServices] IConferenceLinkRepo repo,
            [FromServices] IOptions<ConcurrencyOptions> options)
        {
            var userId = User.Claims.First(x => x.Type == ClaimTypes.NameIdentifier).Value;

            for (var attempt = 0;; attempt++)
            {
                var conferenceLink =
                    (await repo.FindAsync(
                        new ConferenceLinkByParticipant(userId).And(new ConferenceLinkByConference(conferenceId))))
                    .FirstOrDefault();

                if (conferenceLink == null) return ConferenceLinkNotFound();

                var dto = new ChangeConferenceLinkStarDto {Starred = conferenceLink.Starred};
                patch.ApplyTo(dto);

                conferenceLink.Starred = dto.Starred;

                var result = await repo.CreateOrReplaceAsync(conferenceLink);
                switch (result)
                {
                    case OptimisticUpdateResult.Ok:
                        return Ok();
                    case OptimisticUpdateResult.DeletedException:
                        return ConferenceLinkNotFound();
                    case OptimisticUpdateResult.ConcurrencyException when attempt < options.Value.RetryCount:
                        continue;
                    default:
                        return Conflict(new Error(ErrorType.Conflict.ToString(),
                            "The conference link was modified concurrently", "ConferenceLink_Conflict"));
                }
            }
        }

        private NotFoundObjectResult ConferenceLinkNotFound()
        {
            return NotFound(new Error(ErrorType.NotFound.ToString(), "The conference link was not found",
                "ConferenceLink_NotFound"));
        }
    }
}
