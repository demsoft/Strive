using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Strive.Auth;
using Strive.Core;
using Strive.Core.Dto;
using Strive.Core.Services.ConferenceManagement.Gateways;
using Strive.Core.Services.Recording;
using Strive.Core.Services.Recording.Gateways;
using Strive.Core.Services.Recording.Requests;
using Strive.Extensions;
using Strive.Infrastructure.Recording;
using Strive.Models.Request;
using Strive.Models.Response;

namespace Strive.Controllers
{
    [ApiController]
    [Authorize]
    public class RecordingsController : Controller
    {
        private readonly IMediator _mediator;
        private readonly IRecordingRepo _recordings;
        private readonly IRecordingStorage _storage;
        private readonly IConferenceRepo _conferences;
        private readonly IAuthorizationService _authorizationService;
        private readonly RecordingOptions _options;
        private readonly RecorderOptions _recorderOptions;
        private readonly TimeProvider _timeProvider;

        public RecordingsController(IMediator mediator, IRecordingRepo recordings, IRecordingStorage storage,
            IConferenceRepo conferences, IAuthorizationService authorizationService,
            IOptions<RecordingOptions> options, IOptions<RecorderOptions> recorderOptions, TimeProvider timeProvider)
        {
            _mediator = mediator;
            _recordings = recordings;
            _storage = storage;
            _conferences = conferences;
            _authorizationService = authorizationService;
            _options = options.Value;
            _recorderOptions = recorderOptions.Value;
            _timeProvider = timeProvider;
        }

        // GET v1/conference/{conferenceId}/recordings
        [HttpGet("v1/conference/{conferenceId}/recordings")]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<RecordingDto[]>> GetRecordings(string conferenceId)
        {
            var conference = await _conferences.FindById(conferenceId);
            if (conference == null) return ConferenceError.ConferenceNotFound.ToActionResult();

            if (!(await _authorizationService.AuthorizeAsync(User, conference, Operations.Read)).Succeeded)
                return Forbid();

            var recordings = await _recordings.FindOfConference(conferenceId);
            return recordings.Select(RecordingDto.From).ToArray();
        }

        // GET v1/recordings/mine
        /// <summary>
        ///     The recordings that the signed in person started, also of conferences that are closed: the link of a
        ///     recording must not be lost with the meeting
        /// </summary>
        [HttpGet("v1/recordings/mine")]
        public async Task<ActionResult<MyRecordingDto[]>> GetMyRecordings()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Forbid();

            var recordings = await _recordings.FindStartedBy(userId, 100);
            var names = new System.Collections.Generic.Dictionary<string, string?>();
            foreach (var conferenceId in recordings.Select(x => x.ConferenceId).Distinct())
                names[conferenceId] = (await _conferences.FindById(conferenceId))?.Configuration.Name;

            Response.Headers.CacheControl = "no-store";
            return recordings.Select(x => new MyRecordingDto(x.RecordingId, x.ConferenceId, names[x.ConferenceId],
                x.Status, x.StartedAt, x.EndedAt, x.DurationSeconds, x.SizeBytes, x.Visibility, x.ShareToken,
                x.ExpiresAt, x.FailureReason)).ToArray();
        }

        // PATCH v1/recordings/{recordingId}/visibility
        [HttpPatch("v1/recordings/{recordingId}/visibility")]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> SetVisibility(string recordingId, [FromBody] SetRecordingVisibilityDto dto)
        {
            var (recording, forbidden) = await FindAsModerator(recordingId);
            if (forbidden != null) return forbidden;

            var result = await _mediator.Send(new SetRecordingVisibilityRequest(recording!.RecordingId,
                dto.Visibility));
            return result.ToActionResult();
        }

        // DELETE v1/recordings/{recordingId}
        [HttpDelete("v1/recordings/{recordingId}")]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> Delete(string recordingId)
        {
            var (recording, forbidden) = await FindAsModerator(recordingId);
            if (forbidden != null) return forbidden;

            var result = await _mediator.Send(new DeleteRecordingRequest(recording!.RecordingId));
            return result.ToActionResult();
        }

        // GET v1/recordings/shared/{shareToken}
        [HttpGet("v1/recordings/shared/{shareToken}")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<SharedRecordingDto>> GetShared(string shareToken)
        {
            var recording = await _recordings.FindByShareToken(shareToken);

            // the same answer for unknown and unfinished recordings, the token is a secret
            if (recording is not {Status: RecordingStatus.Ready, StorageKey: not null})
                return RecordingError.RecordingNotFound.ToActionResult();

            if (recording.Visibility == RecordingVisibility.SignedIn && User.Identity?.IsAuthenticated != true)
                return Unauthorized();

            var conference = await _conferences.FindById(recording.ConferenceId);
            var url = _storage.GetPlaybackUrl(recording.StorageKey, _options.PlaybackUrlValidFor);

            return new SharedRecordingDto(conference?.Configuration.Name, recording.StartedAt,
                recording.DurationSeconds, url.ToString(), _timeProvider.GetUtcNow().Add(_options.PlaybackUrlValidFor));
        }

        // POST internal/recorder/{recordingId}/report
        [HttpPost("internal/recorder/{recordingId}/report")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult> Report(string recordingId, [FromBody] RecorderReport report)
        {
            if (!IsRecorder()) return Unauthorized();

            var result = await _mediator.Send(new RecorderReportRequest(recordingId, report));
            return result.ToActionResult();
        }

        private bool IsRecorder()
        {
            var secret = _recorderOptions.SharedSecret;
            if (string.IsNullOrEmpty(secret)) return false;

            var provided = Request.Headers[RecorderOptions.SecretHeader].ToString();
            return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided),
                Encoding.UTF8.GetBytes(secret));
        }

        private async Task<(ConferenceRecording?, ActionResult?)> FindAsModerator(string recordingId)
        {
            var recording = await _recordings.FindById(recordingId);
            if (recording == null) return (null, RecordingError.RecordingNotFound.ToActionResult());

            var conference = await _conferences.FindById(recording.ConferenceId);
            if (conference == null) return (null, ConferenceError.ConferenceNotFound.ToActionResult());

            if (!(await _authorizationService.AuthorizeAsync(User, conference, Operations.Update)).Succeeded)
                return (null, Forbid());

            return (recording, null);
        }
    }
}
