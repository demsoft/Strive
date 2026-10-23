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
using Strive.Presenters;

namespace Strive.Controllers
{
    /// <summary>Videos of the creators: reels and lessons that people watch any time.</summary>
    [ApiController]
    public class VideosController : Controller
    {
        private readonly IMediator _mediator;
        private readonly IVideoRepo _videos;
        private readonly ICreatorRepo _creators;
        private readonly VideoPresenter _presenter;

        public VideosController(IMediator mediator, IVideoRepo videos, ICreatorRepo creators, VideoPresenter presenter)
        {
            _mediator = mediator;
            _videos = videos;
            _creators = creators;
            _presenter = presenter;
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
            return new CreatedVideoDto(_presenter.ToDto(created.Video, null, true, false),
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
            return _presenter.ToDto(result.Response!, null, true, false);
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
            return (await _videos.FindOfCreator(creator.CreatorId, 200)).Select(x => _presenter.ToDto(x, null, true, false))
                .ToArray();
        }

        // GET v1/videos/feed?before=...&limit=...
        /// <summary>The latest videos that are ready, for everybody, newest first. The next page starts where this one ended.</summary>
        [HttpGet("v1/videos/feed")]
        [AllowAnonymous]
        public async Task<ActionResult<VideoFeedDto>> Feed([FromQuery] DateTimeOffset? before = null,
            [FromQuery] int limit = 12)
        {
            limit = Math.Clamp(limit, 1, 30);
            var videos = await _videos.FindReady(before, limit);
            var creators = new Dictionary<string, PublicCreatorDto?>();
            var items = new List<VideoDto>();
            foreach (var video in videos)
            {
                if (!creators.TryGetValue(video.CreatorId, out var creator))
                {
                    var profile = await _creators.FindById(video.CreatorId);
                    creators[video.CreatorId] = creator = profile == null ? null : PublicCreatorDto.From(profile);
                }

                items.Add(_presenter.ToDto(video, creator, UserId != null && video.CreatorUserId == UserId, false));
            }

            // a full page: there may be more, after the oldest of these
            Response.Headers.CacheControl = "no-store";
            return new VideoFeedDto(items.ToArray(), videos.Count == limit ? videos[^1].ReadyAt : null);
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
            return _presenter.ToDto(video, creator == null ? null : PublicCreatorDto.From(creator), isOwner, true);
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
    }
}
