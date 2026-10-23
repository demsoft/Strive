using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Strive.Core.Services.Payments;
using Strive.Core.Services.Payments.Gateways;
using Strive.Core.Services.Videos;
using Strive.Extensions;
using Strive.Models.Response;

namespace Strive.Controllers
{
    /// <summary>Videos of the creators: reels and lessons that people watch any time.</summary>
    [ApiController]
    public class VideosController : Controller
    {
        /// <summary>A link plays a video for this long (the page asks for a new one when a person comes back)</summary>
        private static readonly TimeSpan PlaybackLinkValidFor = TimeSpan.FromHours(2);

        private static readonly TimeSpan PosterLinkValidFor = TimeSpan.FromHours(6);

        private readonly IMediator _mediator;
        private readonly IVideoRepo _videos;
        private readonly ICreatorRepo _creators;
        private readonly IVideoStorage _storage;

        public VideosController(IMediator mediator, IVideoRepo videos, ICreatorRepo creators, IVideoStorage storage)
        {
            _mediator = mediator;
            _videos = videos;
            _creators = creators;
            _storage = storage;
        }

        private string? UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // POST v1/videos
        [HttpPost("v1/videos")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<CreatedVideoDto>> Create([FromBody] CreateVideoDto dto)
        {
            if (UserId == null) return Forbid();

            var result = await _mediator.Send(new CreateVideoRequest(UserId, dto.Title, dto.Description, dto.SizeBytes));
            if (!result.Success) return result.ToActionResult();

            Response.Headers.CacheControl = "no-store";
            var created = result.Response!;
            return new CreatedVideoDto(ToDto(created.Video, null, true, false),
                new UploadTicketDto(created.Ticket.Url, created.Ticket.Fields));
        }

        // POST v1/videos/{videoId}/uploaded
        [HttpPost("v1/videos/{videoId}/uploaded")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<VideoDto>> Uploaded(string videoId)
        {
            if (UserId == null) return Forbid();

            var result = await _mediator.Send(new CompleteVideoUploadRequest(UserId, videoId));
            if (!result.Success) return result.ToActionResult();

            Response.Headers.CacheControl = "no-store";
            return ToDto(result.Response!, null, true, false);
        }

        // GET v1/videos/mine
        [HttpGet("v1/videos/mine")]
        [Authorize]
        public async Task<ActionResult<VideoDto[]>> Mine()
        {
            if (UserId == null) return Forbid();

            var creator = await _creators.FindByUserId(UserId);
            if (creator == null) return Array.Empty<VideoDto>();

            Response.Headers.CacheControl = "no-store";
            return (await _videos.FindOfCreator(creator.CreatorId, 200)).Select(x => ToDto(x, null, true, false))
                .ToArray();
        }

        // GET v1/videos/{videoId}
        /// <summary>One video to watch. The creator sees every state of theirs, everybody else only what is ready and free.</summary>
        [HttpGet("v1/videos/{videoId}")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<VideoDto>> Get(string videoId)
        {
            var video = await _videos.FindById(videoId);
            if (video == null) return VideosError.NotFound.ToActionResult();

            var isOwner = UserId != null && video.CreatorUserId == UserId;
            if (!isOwner && (video.Status != VideoStatus.Ready || video.PriceMinor > 0))
                return VideosError.NotFound.ToActionResult();

            var creator = await _creators.FindById(video.CreatorId);

            // the answer depends on who asks, and the links run out
            Response.Headers.CacheControl = "no-store";
            return ToDto(video, creator == null ? null : PublicCreatorDto.From(creator), isOwner, true);
        }

        // DELETE v1/videos/{videoId}
        [HttpDelete("v1/videos/{videoId}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> Delete(string videoId)
        {
            if (UserId == null) return Forbid();

            return (await _mediator.Send(new DeleteVideoRequest(UserId, videoId))).ToActionResult();
        }

        private VideoDto ToDto(Video video, PublicCreatorDto? creator, bool isOwner, bool withPlayback)
        {
            var ready = video.Status == VideoStatus.Ready && _storage.IsConfigured;
            return new VideoDto(video.VideoId, video.Title, video.Description, video.Status.ToString().ToLowerInvariant(),
                isOwner ? video.FailureReason : null, video.DurationSeconds, video.Width, video.Height, video.PriceMinor,
                video.CreatedAt, isOwner,
                ready && video.HasPoster
                    ? _storage.GetPlaybackUrl(VideoRules.PosterKey(video.VideoId), PosterLinkValidFor, "image/jpeg")
                        .ToString()
                    : null,
                ready && withPlayback
                    ? _storage.GetPlaybackUrl(VideoRules.VideoKey(video.VideoId), PlaybackLinkValidFor, "video/mp4")
                        .ToString()
                    : null,
                creator);
        }
    }
}
