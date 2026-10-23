using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Strive.Core.Dto;
using Strive.Core.Interfaces;
using Strive.Core.Services.Payments;
using Strive.Core.Services.Payments.Gateways;
using Strive.Core.Services.Payments.Requests;
using Strive.Core.Services.Payments.Sessions;
using Strive.Core.Services.Payments.UseCases;
using Microsoft.Extensions.Options;

namespace Strive.Core.Services.Videos
{
    public record CreatedVideo(Video Video, UploadTicket Ticket);

    /// <summary>A creator announces a video: it gets a form to send the file straight to the converter</summary>
    /// <param name="PriceMinor">0: free</param>
    public record CreateVideoRequest(string UserId, string Title, string? Description, long SizeBytes,
        long PriceMinor = 0) : IRequest<SuccessOrError<CreatedVideo>>;

    /// <summary>The creator changes the title, the text or the price of a video (a new price is for the next buyers)</summary>
    public record UpdateVideoRequest(string UserId, string VideoId, string Title, string? Description, long PriceMinor)
        : IRequest<SuccessOrError<Video>>;

    /// <summary>Starts the payment of a video. The person is sent to the returned address to pay.</summary>
    public record StartVideoCheckoutRequest(string UserId, string VideoId, string Email, string CallbackUrl)
        : IRequest<SuccessOrError<CheckoutResult>>;

    /// <summary>The creator says the file was sent: the server checks that it arrived and is not too long, then conversion starts</summary>
    public record CompleteVideoUploadRequest(string UserId, string VideoId) : IRequest<SuccessOrError<Video>>;

    public record DeleteVideoRequest(string UserId, string VideoId) : IRequest<SuccessOrError<Unit>>;

    /// <summary>The background work: copy converted videos to the storage, give up on the ones that never finish</summary>
    /// <returns>The number of videos that became ready</returns>
    /// <param name="OnlyVideoId">Look at this video only (null: all that are waiting)</param>
    public record ProcessVideosRequest(string? OnlyVideoId = null) : IRequest<SuccessOrError<int>>;

    public class CreateVideoUseCase : IRequestHandler<CreateVideoRequest, SuccessOrError<CreatedVideo>>
    {
        private readonly ICreatorRepo _creators;
        private readonly IVideoRepo _videos;
        private readonly IVideoConverter _converter;
        private readonly IVideoStorage _storage;
        private readonly PaymentsOptions _payments;
        private readonly TimeProvider _timeProvider;

        public CreateVideoUseCase(ICreatorRepo creators, IVideoRepo videos, IVideoConverter converter,
            IVideoStorage storage, IOptions<PaymentsOptions> payments, TimeProvider timeProvider)
        {
            _payments = payments.Value;
            _creators = creators;
            _videos = videos;
            _converter = converter;
            _storage = storage;
            _timeProvider = timeProvider;
        }

        public async Task<SuccessOrError<CreatedVideo>> Handle(CreateVideoRequest request,
            CancellationToken cancellationToken)
        {
            if (!_converter.IsConfigured || !_storage.IsConfigured) return VideosError.NotEnabled;

            var creator = await _creators.FindByUserId(request.UserId);
            if (creator == null) return PaymentsError.CreatorNotFound;

            var title = request.Title?.Trim() ?? "";
            if (title.Length is < VideoRules.MinTitleLength or > VideoRules.MaxTitleLength)
                return VideosError.Invalid(
                    $"The title must have {VideoRules.MinTitleLength} to {VideoRules.MaxTitleLength} characters.");

            var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            if (description is {Length: > VideoRules.MaxDescriptionLength})
                return VideosError.Invalid(
                    $"The description can have {VideoRules.MaxDescriptionLength} characters at most.");

            if (request.SizeBytes is <= 0 or > VideoRules.MaxUploadBytes)
                return VideosError.Invalid($"The video file can be {VideoRules.MaxUploadBytes / (1024 * 1024)} MB at most.");

            var priceError = VideoPrices.Problem(request.PriceMinor, creator, _payments);
            if (priceError != null) return priceError;

            if (await _videos.CountOfCreator(creator.CreatorId) >= VideoRules.MaxVideosPerCreator)
                return VideosError.Invalid("You have reached the number of videos you can keep. Delete one first.");

            var video = new Video(Guid.NewGuid().ToString("N"), creator.CreatorId, request.UserId, title,
                _timeProvider.GetUtcNow()) {Description = description, PriceMinor = request.PriceMinor};
            await _videos.Create(video);
            return new CreatedVideo(video, _converter.CreateUploadTicket(video.VideoId));
        }
    }

    public class CompleteVideoUploadUseCase : IRequestHandler<CompleteVideoUploadRequest, SuccessOrError<Video>>
    {
        private readonly IVideoRepo _videos;
        private readonly IVideoConverter _converter;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<CompleteVideoUploadUseCase> _logger;

        public CompleteVideoUploadUseCase(IVideoRepo videos, IVideoConverter converter, TimeProvider timeProvider,
            ILogger<CompleteVideoUploadUseCase> logger)
        {
            _videos = videos;
            _converter = converter;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<SuccessOrError<Video>> Handle(CompleteVideoUploadRequest request,
            CancellationToken cancellationToken)
        {
            var video = await _videos.FindById(request.VideoId);
            // somebody else's video does not exist for you
            if (video == null || video.CreatorUserId != request.UserId) return VideosError.NotFound;

            // said twice, or already further: nothing to do
            if (video.Status != VideoStatus.Uploading) return video;

            ConversionStatus status;
            try
            {
                status = await _converter.GetStatus(video.VideoId, cancellationToken);
            }
            catch (VideoServiceException e)
            {
                _logger.LogWarning(e, "The video {videoId} could not be looked up at the converter", video.VideoId);
                return VideosError.Invalid("The video service did not answer. Try again in a moment.");
            }

            if (!status.Exists) return VideosError.UploadNotFound;

            // the page says what the file is, the converter knows: nothing longer than allowed is kept
            if (status.DurationSeconds is { } seconds && seconds > VideoRules.MaxSeconds)
            {
                video.Status = VideoStatus.Failed;
                video.FailureReason =
                    $"The video is longer than {VideoRules.MaxSeconds / 60} minutes. Cut it and upload it again.";
                await _videos.Update(video);
                await TryDelete(video.VideoId, cancellationToken);
                return video;
            }

            video.Status = VideoStatus.Processing;
            video.ProcessingSince = _timeProvider.GetUtcNow();
            video.DurationSeconds = status.DurationSeconds;
            await _videos.Update(video);
            return video;
        }

        private async Task TryDelete(string videoId, CancellationToken cancellationToken)
        {
            try
            {
                await _converter.Delete(videoId, cancellationToken);
            }
            catch (VideoServiceException e)
            {
                _logger.LogWarning(e, "The video {videoId} could not be deleted at the converter", videoId);
            }
        }
    }

    public class DeleteVideoUseCase : IRequestHandler<DeleteVideoRequest, SuccessOrError<Unit>>
    {
        private readonly IVideoRepo _videos;
        private readonly IVideoConverter _converter;
        private readonly IVideoStorage _storage;
        private readonly IVideoEntitlementRepo _entitlements;
        private readonly ILogger<DeleteVideoUseCase> _logger;

        public DeleteVideoUseCase(IVideoRepo videos, IVideoConverter converter, IVideoStorage storage,
            IVideoEntitlementRepo entitlements, ILogger<DeleteVideoUseCase> logger)
        {
            _entitlements = entitlements;
            _videos = videos;
            _converter = converter;
            _storage = storage;
            _logger = logger;
        }

        public async Task<SuccessOrError<Unit>> Handle(DeleteVideoRequest request, CancellationToken cancellationToken)
        {
            var video = await _videos.FindById(request.VideoId);
            if (video == null || video.CreatorUserId != request.UserId) return VideosError.NotFound;

            // people paid for it: it stays (the creator may make it free)
            if (await _entitlements.CountOfVideo(video.VideoId) > 0) return VideosError.HasBuyers;

            // the files go first: a video that is gone from the list must not stay in the storage
            await Cleanup(video.VideoId, cancellationToken);
            await _videos.Delete(video.VideoId);
            return Unit.Value;
        }

        private async Task Cleanup(string videoId, CancellationToken cancellationToken)
        {
            try
            {
                if (_storage.IsConfigured)
                {
                    await _storage.Delete(VideoRules.VideoKey(videoId), cancellationToken);
                    await _storage.Delete(VideoRules.PosterKey(videoId), cancellationToken);
                }

                if (_converter.IsConfigured) await _converter.Delete(videoId, cancellationToken);
            }
            catch (Exception e) when (e is VideoServiceException or System.IO.IOException)
            {
                _logger.LogWarning(e, "The files of video {videoId} could not all be deleted", videoId);
            }
        }
    }

    public class ProcessVideosUseCase : IRequestHandler<ProcessVideosRequest, SuccessOrError<int>>
    {
        private static readonly TimeSpan PosterWait = TimeSpan.FromMinutes(5);

        private readonly IVideoRepo _videos;
        private readonly IVideoConverter _converter;
        private readonly IVideoStorage _storage;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<ProcessVideosUseCase> _logger;

        public ProcessVideosUseCase(IVideoRepo videos, IVideoConverter converter, IVideoStorage storage,
            TimeProvider timeProvider, ILogger<ProcessVideosUseCase> logger)
        {
            _videos = videos;
            _converter = converter;
            _storage = storage;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<SuccessOrError<int>> Handle(ProcessVideosRequest request, CancellationToken cancellationToken)
        {
            if (!_converter.IsConfigured || !_storage.IsConfigured) return 0;

            var now = _timeProvider.GetUtcNow();
            var ready = 0;

            // files that never arrived
            foreach (var video in await Waiting(request, VideoStatus.Uploading))
            {
                if (now - video.CreatedAt < VideoRules.UploadGivenUpAfter) continue;
                await Fail(video, "The upload was not finished. Try again.", cancellationToken);
            }

            foreach (var video in await Waiting(request, VideoStatus.Processing))
                try
                {
                    if (await Finish(video, now, cancellationToken)) ready++;
                }
                catch (Exception e) when (e is VideoServiceException or System.IO.IOException)
                {
                    _logger.LogWarning(e, "Video {videoId} could not be finished", video.VideoId);
                    video.CopyAttempts++;
                    if (video.CopyAttempts >= VideoRules.MaxCopyAttempts)
                        await Fail(video, "The video could not be saved. Upload it again.", cancellationToken);
                    else
                        await _videos.Update(video);
                }

            return ready;
        }

        private async Task<IReadOnlyList<Video>> Waiting(ProcessVideosRequest request, VideoStatus status)
        {
            if (request.OnlyVideoId == null) return await _videos.FindByStatus(status, 50);

            var one = await _videos.FindById(request.OnlyVideoId);
            return one is { } video && video.Status == status ? new[] {video} : Array.Empty<Video>();
        }

        /// <returns>True if the video became ready</returns>
        private async Task<bool> Finish(Video video, DateTimeOffset now, CancellationToken cancellationToken)
        {
            var age = now - (video.ProcessingSince ?? video.CreatedAt);
            var status = await _converter.GetStatus(video.VideoId, cancellationToken);

            if (!status.Exists)
            {
                await Fail(video, "The video was lost. Upload it again.", cancellationToken);
                return false;
            }

            // the converter makes the video and the cover separately: the cover may follow a little later
            var waitForPoster = status.PosterUrl == null && age < PosterWait;
            if (status.VideoUrl == null || waitForPoster)
            {
                if (age > VideoRules.ConversionGivenUpAfter)
                    await Fail(video, "The video could not be converted. Check that it plays on your phone, or try another file.",
                        cancellationToken);
                return false;
            }

            long size;
            using (var file = await _converter.Open(status.VideoUrl, cancellationToken))
            {
                size = file.Length;
                await _storage.Put(VideoRules.VideoKey(video.VideoId), file.Content, file.Length, "video/mp4",
                    cancellationToken);
            }

            if (status.PosterUrl != null)
                using (var poster = await _converter.Open(status.PosterUrl, cancellationToken))
                    await _storage.Put(VideoRules.PosterKey(video.VideoId), poster.Content, poster.Length, "image/jpeg",
                        cancellationToken);

            video.Status = VideoStatus.Ready;
            video.HasPoster = status.PosterUrl != null;
            video.SizeBytes = size;
            video.Width = status.Width ?? video.Width;
            video.Height = status.Height ?? video.Height;
            video.DurationSeconds = status.DurationSeconds ?? video.DurationSeconds;
            video.ReadyAt = now;
            video.FailureReason = null;
            await _videos.Update(video);

            // what is at the converter is not needed any more: nothing piles up there
            try
            {
                await _converter.Delete(video.VideoId, cancellationToken);
            }
            catch (VideoServiceException e)
            {
                _logger.LogWarning(e, "Video {videoId} is ready but could not be deleted at the converter", video.VideoId);
            }

            return true;
        }

        private async Task Fail(Video video, string reason, CancellationToken cancellationToken)
        {
            video.Status = VideoStatus.Failed;
            video.FailureReason = reason;
            await _videos.Update(video);
            try
            {
                await _converter.Delete(video.VideoId, cancellationToken);
            }
            catch (VideoServiceException e)
            {
                _logger.LogWarning(e, "Failed video {videoId} could not be deleted at the converter", video.VideoId);
            }
        }
    }

    public static class VideoPrices
    {
        /// <summary>Why this price cannot be set (null: it can). A price needs a creator who can be paid.</summary>
        public static Error? Problem(long priceMinor, CreatorProfile creator, PaymentsOptions payments)
        {
            if (!VideoRules.IsValidPrice(priceMinor))
                return VideosError.Invalid(
                    $"The price must be free, or between {SessionRules.MinPriceMinor / 100} and {SessionRules.MaxPriceMinor / 100} {creator.Currency}.");

            if (priceMinor > 0 && (!payments.Enabled || !creator.CanSell))
                return PaymentsError.CreatorCannotSell;

            return null;
        }
    }

    public class UpdateVideoUseCase : IRequestHandler<UpdateVideoRequest, SuccessOrError<Video>>
    {
        private readonly IVideoRepo _videos;
        private readonly ICreatorRepo _creators;
        private readonly PaymentsOptions _payments;

        public UpdateVideoUseCase(IVideoRepo videos, ICreatorRepo creators, IOptions<PaymentsOptions> payments)
        {
            _videos = videos;
            _creators = creators;
            _payments = payments.Value;
        }

        public async Task<SuccessOrError<Video>> Handle(UpdateVideoRequest request, CancellationToken cancellationToken)
        {
            var video = await _videos.FindById(request.VideoId);
            if (video == null || video.CreatorUserId != request.UserId) return VideosError.NotFound;

            var creator = await _creators.FindById(video.CreatorId);
            if (creator == null) return PaymentsError.CreatorNotFound;

            var title = request.Title?.Trim() ?? "";
            if (title.Length is < VideoRules.MinTitleLength or > VideoRules.MaxTitleLength)
                return VideosError.Invalid(
                    $"The title must have {VideoRules.MinTitleLength} to {VideoRules.MaxTitleLength} characters.");

            var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            if (description is {Length: > VideoRules.MaxDescriptionLength})
                return VideosError.Invalid(
                    $"The description can have {VideoRules.MaxDescriptionLength} characters at most.");

            // keeping the price as it is needs no check: a creator who cannot be paid any more can still edit the text
            if (request.PriceMinor != video.PriceMinor)
            {
                var priceError = VideoPrices.Problem(request.PriceMinor, creator, _payments);
                if (priceError != null) return priceError;
            }

            video.Title = title;
            video.Description = description;
            video.PriceMinor = request.PriceMinor;
            await _videos.Update(video);
            return video;
        }
    }

    public class StartVideoCheckoutUseCase : IRequestHandler<StartVideoCheckoutRequest, SuccessOrError<CheckoutResult>>
    {
        private readonly IPaymentProvider _provider;
        private readonly ICreatorRepo _creators;
        private readonly IVideoRepo _videos;
        private readonly IOrderRepo _orders;
        private readonly VideoAccess _access;
        private readonly PaymentsOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<StartVideoCheckoutUseCase> _logger;

        public StartVideoCheckoutUseCase(IPaymentProvider provider, ICreatorRepo creators, IVideoRepo videos,
            IOrderRepo orders, VideoAccess access, IOptions<PaymentsOptions> options, TimeProvider timeProvider,
            ILogger<StartVideoCheckoutUseCase> logger)
        {
            _provider = provider;
            _creators = creators;
            _videos = videos;
            _orders = orders;
            _access = access;
            _options = options.Value;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<SuccessOrError<CheckoutResult>> Handle(StartVideoCheckoutRequest request,
            CancellationToken cancellationToken)
        {
            if (!_options.Enabled) return PaymentsError.NotEnabled;
            if (!SessionRules.IsValidEmail(request.Email)) return PaymentsError.InvalidEmail;

            var video = await _videos.FindById(request.VideoId);
            if (video == null || video.Status != VideoStatus.Ready) return VideosError.NotFound;
            if (video.PriceMinor <= 0) return VideosError.NotForSale;
            if (video.CreatorUserId == request.UserId) return PaymentsError.OwnPass;

            // bought already, or covered by a running pass of the creator: nobody pays twice
            if (await _access.CanWatch(video, request.UserId)) return new CheckoutResult(null, null, true);

            var creator = await _creators.FindById(video.CreatorId);
            if (creator == null || !creator.CanSell) return PaymentsError.CreatorCannotSell;

            var order = new TicketOrder(Guid.NewGuid().ToString("N"), SessionRules.NewReference(), "", "",
                request.UserId, request.Email.Trim(), video.PriceMinor, creator.Currency,
                _options.PlatformCommissionPercent, _timeProvider.GetUtcNow())
            {
                Kind = OrderKind.Video,
                VideoId = video.VideoId,
                Shares = new System.Collections.Generic.List<RevenueShare>
                {
                    new(creator.CreatorId, creator.ProviderSubaccountCode!, 100),
                },
            };
            await _orders.Create(order);

            try
            {
                var checkout = await _provider.InitializeCheckout(new CheckoutRequest(order.Reference, order.Email,
                    order.AmountMinor, order.Currency, request.CallbackUrl, creator.ProviderSubaccountCode!,
                    PriceSplit.PlatformAmount(order.AmountMinor, order.CommissionPercent),
                    new System.Collections.Generic.Dictionary<string, string>
                        {{"kind", "video"}, {"videoId", video.VideoId}, {"orderId", order.OrderId}}), cancellationToken);
                return new CheckoutResult(checkout.AuthorizationUrl, order.Reference, false);
            }
            catch (PaymentProviderException e)
            {
                _logger.LogWarning(e, "The checkout of video order {orderId} could not be started", order.OrderId);
                await _orders.MarkFailed(order.Reference, "The checkout could not be started.");
                return PaymentsError.ProviderUnavailable;
            }
        }
    }
}
