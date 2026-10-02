using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using SpeciVacation;
using Strive.Controllers;
using Strive.Core;
using Strive.Core.Domain.Entities;
using Strive.Core.Interfaces.Gateways;
using Strive.Core.Interfaces.Gateways.Repositories;
using Strive.Models.Request;
using Xunit;

namespace Strive.Tests.Controllers
{
    public class ConferenceLinkControllerTests
    {
        private const string ParticipantId = "123";
        private const string ConferenceId = "45";
        private const int RetryCount = 2;

        private readonly Mock<IConferenceLinkRepo> _repo = new();

        private static ConferenceLinkController Create()
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[] {new Claim(ClaimTypes.NameIdentifier, ParticipantId)}));
            return new ConferenceLinkController
            {
                ControllerContext = new ControllerContext {HttpContext = new DefaultHttpContext {User = user}},
            };
        }

        private static JsonPatchDocument<ChangeConferenceLinkStarDto> StarPatch()
        {
            var patch = new JsonPatchDocument<ChangeConferenceLinkStarDto>();
            patch.Replace(x => x.Starred, true);
            return patch;
        }

        private void SetupLink()
        {
            _repo.Setup(x => x.FindAsync(It.IsAny<ISpecification<ConferenceLink>>()))
                .ReturnsAsync(() => new List<ConferenceLink> {new(ParticipantId, ConferenceId)});
        }

        private Task<ActionResult> Patch()
        {
            return Create().PatchConferenceLink(ConferenceId, StarPatch(), _repo.Object,
                Options.Create(new ConcurrencyOptions {RetryCount = RetryCount}));
        }

        [Fact]
        public async Task PatchConferenceLink_Updated_ReturnOkAndSaveStarred()
        {
            SetupLink();
            _repo.Setup(x => x.CreateOrReplaceAsync(It.IsAny<ConferenceLink>()))
                .ReturnsAsync(OptimisticUpdateResult.Ok);

            var result = await Patch();

            Assert.IsType<OkResult>(result);
            _repo.Verify(x => x.CreateOrReplaceAsync(It.Is<ConferenceLink>(link => link.Starred)), Times.Once);
        }

        [Fact]
        public async Task PatchConferenceLink_ConcurrencyConflictOnce_RetryAndReturnOk()
        {
            SetupLink();
            _repo.SetupSequence(x => x.CreateOrReplaceAsync(It.IsAny<ConferenceLink>()))
                .ReturnsAsync(OptimisticUpdateResult.ConcurrencyException).ReturnsAsync(OptimisticUpdateResult.Ok);

            var result = await Patch();

            Assert.IsType<OkResult>(result);
            _repo.Verify(x => x.CreateOrReplaceAsync(It.IsAny<ConferenceLink>()), Times.Exactly(2));
        }

        [Fact]
        public async Task PatchConferenceLink_ConcurrencyConflictOnEveryAttempt_ReturnConflict()
        {
            SetupLink();
            _repo.Setup(x => x.CreateOrReplaceAsync(It.IsAny<ConferenceLink>()))
                .ReturnsAsync(OptimisticUpdateResult.ConcurrencyException);

            var result = await Patch();

            Assert.IsType<ConflictObjectResult>(result);
            _repo.Verify(x => x.CreateOrReplaceAsync(It.IsAny<ConferenceLink>()), Times.Exactly(RetryCount + 1));
        }

        [Fact]
        public async Task PatchConferenceLink_DeletedConcurrently_ReturnNotFound()
        {
            SetupLink();
            _repo.Setup(x => x.CreateOrReplaceAsync(It.IsAny<ConferenceLink>()))
                .ReturnsAsync(OptimisticUpdateResult.DeletedException);

            var result = await Patch();

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task PatchConferenceLink_LinkDoesNotExist_ReturnNotFound()
        {
            _repo.Setup(x => x.FindAsync(It.IsAny<ISpecification<ConferenceLink>>()))
                .ReturnsAsync(new List<ConferenceLink>());

            var result = await Patch();

            Assert.IsType<NotFoundObjectResult>(result);
            _repo.Verify(x => x.CreateOrReplaceAsync(It.IsAny<ConferenceLink>()), Times.Never);
        }
    }
}
