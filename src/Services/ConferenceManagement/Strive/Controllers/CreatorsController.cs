using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Strive.Core.Services.Payments;
using Strive.Core.Services.Payments.Gateways;
using Strive.Core.Services.Payments.Requests;
using Strive.Extensions;
using Strive.Models.Response;

namespace Strive.Controllers
{
    /// <summary>
    ///     Creators: people who host paid sessions. They add the bank account their earnings are paid into.
    /// </summary>
    [ApiController]
    [Authorize]
    public class CreatorsController : Controller
    {
        private readonly IMediator _mediator;
        private readonly ICreatorRepo _creators;
        private readonly PaymentsOptions _options;
        private readonly ICreatorPhotoRepo _photos;

        public CreatorsController(IMediator mediator, ICreatorRepo creators, ICreatorPhotoRepo photos,
            IOptions<PaymentsOptions> options)
        {
            _photos = photos;
            _mediator = mediator;
            _creators = creators;
            _options = options.Value;
        }

        private string? UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // GET v1/creators/config
        [HttpGet("v1/creators/config")]
        public ActionResult<PaymentsConfigDto> GetConfig()
        {
            return new PaymentsConfigDto(_options.Enabled, _options.Country, _options.Currency,
                _options.PlatformCommissionPercent);
        }

        // GET v1/creators/banks
        [HttpGet("v1/creators/banks")]
        public async Task<ActionResult<BankDto[]>> GetBanks()
        {
            var result = await _mediator.Send(new ListBanksRequest());
            if (!result.Success) return result.ToActionResult();

            Response.Headers.CacheControl = "private, max-age=3600";
            return result.Response!.Select(x => new BankDto(x.Code, x.Name)).ToArray();
        }

        // POST v1/creators/resolve-account
        [HttpPost("v1/creators/resolve-account")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ResolvedAccountDto>> ResolveAccount([FromBody] ResolveAccountDto dto)
        {
            if (UserId == null) return Forbid();

            var result = await _mediator.Send(new ResolveBankAccountRequest(dto.BankCode, dto.AccountNumber));
            if (!result.Success) return result.ToActionResult();

            Response.Headers.CacheControl = "no-store";
            return new ResolvedAccountDto(result.Response!.AccountName);
        }

        // GET v1/creators/me
        /// <summary>
        ///     The creator profile of the signed in person. A fan has none: that is a normal answer (204, no content), not an
        ///     error, so that it does not show up as a red line in the browser.
        /// </summary>
        [HttpGet("v1/creators/me")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<ActionResult<CreatorProfileDto>> GetMine()
        {
            if (UserId == null) return Forbid();

            var profile = await _creators.FindByUserId(UserId);
            if (profile == null) return NoContent();

            Response.Headers.CacheControl = "no-store";
            return CreatorProfileDto.From(profile);
        }

        // PUT v1/creators/me
        [HttpPut("v1/creators/me")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<CreatorProfileDto>> SaveMine([FromBody] SaveCreatorDto dto)
        {
            if (UserId == null) return Forbid();

            var result = await _mediator.Send(new SaveCreatorProfileRequest(UserId, dto.DisplayName, dto.Bio,
                dto.BankCode, dto.AccountNumber));
            if (!result.Success) return result.ToActionResult();

            return CreatorProfileDto.From(result.Response!);
        }

        // PUT v1/creators/me/pass
        [HttpPut("v1/creators/me/pass")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<CreatorProfileDto>> SetPassPrice([FromBody] SetPassPriceDto dto)
        {
            if (UserId == null) return Forbid();

            var result = await _mediator.Send(new SetPassPriceRequest(UserId, dto.PriceMinor));
            if (!result.Success) return result.ToActionResult();

            return CreatorProfileDto.From(result.Response!);
        }

        // PUT v1/creators/me/slug
        [HttpPut("v1/creators/me/slug")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<CreatorProfileDto>> SetSlug([FromBody] SetSlugDto dto)
        {
            if (UserId == null) return Forbid();

            var result = await _mediator.Send(new SetSlugRequest(UserId, dto.Slug));
            if (!result.Success) return result.ToActionResult();

            return CreatorProfileDto.From(result.Response!);
        }

        // PUT v1/creators/me/photo
        /// <summary>The body is the picture itself (jpeg, png or webp). What it is is decided by its bytes.</summary>
        [HttpPut("v1/creators/me/photo")]
        [RequestSizeLimit(PhotoRules.MaxBytes + 4096)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<CreatorProfileDto>> SetPhoto()
        {
            if (UserId == null) return Forbid();

            // read one byte more than allowed: that is how a too big picture is noticed without reading it all
            using var buffer = new System.IO.MemoryStream();
            var chunk = new byte[16 * 1024];
            int read;
            while (buffer.Length <= PhotoRules.MaxBytes &&
                   (read = await Request.Body.ReadAsync(chunk, HttpContext.RequestAborted)) > 0)
                buffer.Write(chunk, 0, read);

            var result = await _mediator.Send(new SetCreatorPhotoRequest(UserId, buffer.ToArray()));
            if (!result.Success) return result.ToActionResult();

            return CreatorProfileDto.From(result.Response!);
        }

        // DELETE v1/creators/me/photo
        [HttpDelete("v1/creators/me/photo")]
        public async Task<ActionResult<CreatorProfileDto>> RemovePhoto()
        {
            if (UserId == null) return Forbid();

            var result = await _mediator.Send(new SetCreatorPhotoRequest(UserId, null));
            if (!result.Success) return result.ToActionResult();

            return CreatorProfileDto.From(result.Response!);
        }

        // GET v1/creators/{creatorIdOrSlug}/photo
        /// <summary>The picture of a creator, for everybody. The address carries the version, so it may be cached.</summary>
        [HttpGet("v1/creators/{creatorId}/photo")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPhoto(string creatorId)
        {
            var creator = await _creators.FindById(creatorId) ??
                          await _creators.FindBySlug(Strive.Core.Services.Payments.SlugRules.Normalize(creatorId));
            if (creator?.PhotoVersion == null) return NotFound();

            var etag = $"\"{creator.PhotoVersion}\"";
            if (Request.Headers.IfNoneMatch.ToString() == etag) return StatusCode(StatusCodes.Status304NotModified);

            var photo = await _photos.Find(creator.CreatorId);
            if (photo == null) return NotFound();

            Response.Headers.ETag = etag;
            Response.Headers.CacheControl = "public, max-age=3600";
            // it is a picture and nothing else, whatever the browser would like to make of it
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
            return File(photo.Data, photo.ContentType);
        }

        // GET v1/creators/{creatorId}
        [HttpGet("v1/creators/{creatorId}")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PublicCreatorDto>> GetPublic(string creatorId)
        {
            var profile = await _creators.FindById(creatorId);
            if (profile is not {CanSell: true}) return PaymentsError.CreatorNotFound.ToActionResult();

            return PublicCreatorDto.From(profile);
        }
    }
}
