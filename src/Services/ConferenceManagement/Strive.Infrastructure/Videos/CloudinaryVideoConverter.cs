using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Strive.Core.Services.Payments;
using Strive.Core.Services.Videos;
using Strive.Infrastructure.Photos;

namespace Strive.Infrastructure.Videos
{
    /// <summary>
    ///     Converts the videos at Cloudinary: the creator's browser sends the file there with a signed form, Cloudinary makes
    ///     an h264/aac mp4 (at most 1280 pixels) and a cover picture, the server asks what is ready and copies it away.
    /// </summary>
    public class CloudinaryVideoConverter : IVideoConverter
    {
        /// <summary>
        ///     What Cloudinary makes of every upload (written as it takes it, "|" separates the two): the video fitted into a
        ///     1280 x 1280 box (so 1920x1080 becomes 1280x720 and 1080x1920 becomes 720x1280) as h264 and aac in an mp4, and
        ///     the first second as a jpeg of at most 720 pixels.
        /// </summary>
        public const string Eager = "c_limit,w_1280,h_1280,vc_h264,ac_aac,q_auto/mp4|so_1,c_limit,w_720,h_720/jpg";

        private const string AllowedFormats = "mp4,mov,webm,m4v,3gp,mkv,avi";

        private readonly HttpClient _client;
        private readonly CloudinaryOptions _options;
        private readonly VideoOptions _videoOptions;
        private readonly TimeProvider _timeProvider;

        public CloudinaryVideoConverter(HttpClient client, IOptions<CloudinaryOptions> options,
            IOptions<VideoOptions> videoOptions, TimeProvider timeProvider)
        {
            _client = client;
            _options = options.Value;
            _videoOptions = videoOptions.Value;
            _timeProvider = timeProvider;
        }

        public bool IsConfigured => _options.IsConfigured;

        public string PublicIdOf(string videoId) => $"{_videoOptions.Folder.Trim('/')}/{videoId}";

        private string Api(string path) =>
            $"{_options.ApiBaseUrl.TrimEnd('/')}/v1_1/{Uri.EscapeDataString(_options.CloudName!)}/{path}";

        public UploadTicket CreateUploadTicket(string videoId)
        {
            var signed = new Dictionary<string, string>
            {
                ["allowed_formats"] = AllowedFormats,
                ["eager"] = Eager,
                ["eager_async"] = "true",
                ["public_id"] = PublicIdOf(videoId),
                ["timestamp"] = _timeProvider.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            };

            var fields = new Dictionary<string, string>(signed)
            {
                ["api_key"] = _options.ApiKey!,
                ["signature"] = CloudinaryPhotoStorage.Sign(signed, _options.ApiSecret!),
            };
            return new UploadTicket(Api("video/upload"), fields);
        }

        public async Task<ConversionStatus> GetStatus(string videoId, CancellationToken cancellationToken = default)
        {
            var path = string.Join("/", PublicIdOf(videoId).Split('/').Select(Uri.EscapeDataString));
            using var request = new HttpRequestMessage(HttpMethod.Get, Api($"resources/video/upload/{path}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ApiKey}:{_options.ApiSecret}")));

            HttpResponseMessage response;
            try
            {
                response = await _client.SendAsync(request, cancellationToken);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                throw new VideoServiceException("Cloudinary could not be reached.", e);
            }

            using (response)
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                    return new ConversionStatus(false, null, null, null, null, null, null);

                var text = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                    throw new VideoServiceException($"Cloudinary answered {(int) response.StatusCode}.");

                ResourceDto? resource;
                try
                {
                    resource = JsonSerializer.Deserialize<ResourceDto>(text, Json);
                }
                catch (JsonException e)
                {
                    throw new VideoServiceException("Cloudinary answered something unreadable.", e);
                }

                if (resource == null) throw new VideoServiceException("Cloudinary answered nothing.");

                Uri? Derived(params string[] formats) => resource.Derived?
                    .Where(x => formats.Contains((x.Format ?? "").ToLowerInvariant()) &&
                                !string.IsNullOrEmpty(x.SecureUrl ?? x.Url))
                    .Select(x => new Uri(x.SecureUrl ?? x.Url!))
                    .FirstOrDefault();

                return new ConversionStatus(true, resource.Duration, resource.Bytes, resource.Width, resource.Height,
                    Derived("mp4"), Derived("jpg", "jpeg"));
            }
        }

        public async Task<RemoteFile> Open(Uri url, CancellationToken cancellationToken = default)
        {
            // only what Cloudinary delivers is fetched: the address comes from its answer, but is checked anyway
            if (url.Scheme != Uri.UriSchemeHttps ||
                !url.Host.EndsWith(new Uri(_options.DeliveryBaseUrl).Host, StringComparison.OrdinalIgnoreCase))
                throw new VideoServiceException("The converted file is not at Cloudinary.");

            HttpResponseMessage response;
            try
            {
                response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                throw new VideoServiceException("Cloudinary could not be reached.", e);
            }

            if (!response.IsSuccessStatusCode)
            {
                // 423: Cloudinary is still making it
                var code = (int) response.StatusCode;
                response.Dispose();
                throw new VideoServiceException($"Cloudinary answered {code} for the converted file.");
            }

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            if (response.Content.Headers.ContentLength is { } length) return new RemoteFile(stream, length, response);

            // the length is needed to store it: without it the file goes through a temporary file
            var path = Path.GetTempFileName();
            var temp = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920,
                FileOptions.DeleteOnClose);
            await using (stream)
            {
                await stream.CopyToAsync(temp, cancellationToken);
            }

            response.Dispose();
            temp.Position = 0;
            return new RemoteFile(temp, temp.Length);
        }

        public async Task Delete(string videoId, CancellationToken cancellationToken = default)
        {
            var signed = new Dictionary<string, string>
            {
                ["invalidate"] = "true",
                ["public_id"] = PublicIdOf(videoId),
                ["timestamp"] = _timeProvider.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            };

            using var form = new MultipartFormDataContent();
            foreach (var (key, value) in signed) form.Add(new StringContent(value), key);
            form.Add(new StringContent(_options.ApiKey!), "api_key");
            form.Add(new StringContent(CloudinaryPhotoStorage.Sign(signed, _options.ApiSecret!)), "signature");

            try
            {
                // "not found" is an answer with status 200: a file that is not there needs no deleting
                using var response = await _client.PostAsync(Api("video/destroy"), form, cancellationToken);
                if (!response.IsSuccessStatusCode)
                    throw new VideoServiceException($"Cloudinary answered {(int) response.StatusCode}.");
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                throw new VideoServiceException("Cloudinary could not be reached.", e);
            }
        }

        private static readonly JsonSerializerOptions Json = new() {PropertyNameCaseInsensitive = true};

        private class ResourceDto
        {
            [JsonPropertyName("duration")] public double? Duration { get; set; }

            [JsonPropertyName("bytes")] public long? Bytes { get; set; }

            [JsonPropertyName("width")] public int? Width { get; set; }

            [JsonPropertyName("height")] public int? Height { get; set; }

            [JsonPropertyName("derived")] public List<DerivedDto>? Derived { get; set; }
        }

        private class DerivedDto
        {
            [JsonPropertyName("format")] public string? Format { get; set; }

            [JsonPropertyName("url")] public string? Url { get; set; }

            [JsonPropertyName("secure_url")] public string? SecureUrl { get; set; }
        }
    }
}
