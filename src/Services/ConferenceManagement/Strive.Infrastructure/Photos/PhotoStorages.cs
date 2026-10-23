using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Strive.Core.Services.Payments;
using Strive.Core.Services.Payments.Gateways;

namespace Strive.Infrastructure.Photos
{
    /// <summary>The photos in the database: used while no Cloudinary account is set up.</summary>
    public class MongoPhotoStorage : IPhotoStorage
    {
        private readonly ICreatorPhotoRepo _photos;
        private readonly TimeProvider _timeProvider;

        public MongoPhotoStorage(ICreatorPhotoRepo photos, TimeProvider timeProvider)
        {
            _photos = photos;
            _timeProvider = timeProvider;
        }

        public async Task<StoredPhoto> Save(string creatorId, string contentType, byte[] data,
            CancellationToken cancellationToken = default)
        {
            var now = _timeProvider.GetUtcNow();
            await _photos.Save(new CreatorPhoto(creatorId, contentType, data, now));
            return new StoredPhoto(now.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture), null);
        }

        public Task Remove(string creatorId, string? publicId, CancellationToken cancellationToken = default) =>
            _photos.Delete(creatorId);

        public string? RedirectUrl(string publicId, string version, int size) => null;
    }

    /// <summary>
    ///     The photos at Cloudinary: uploaded by the server with a signature (the browser never sees the secret) and
    ///     delivered by its CDN, at the size that is asked for, in the lightest format the browser understands.
    /// </summary>
    public class CloudinaryPhotoStorage : IPhotoStorage
    {
        private readonly HttpClient _client;
        private readonly CloudinaryOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<CloudinaryPhotoStorage> _logger;

        public CloudinaryPhotoStorage(HttpClient client, IOptions<CloudinaryOptions> options, TimeProvider timeProvider,
            ILogger<CloudinaryPhotoStorage> logger)
        {
            _client = client;
            _options = options.Value;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        /// <summary>One picture per creator: a new one replaces it under the same name, so nothing is left behind</summary>
        public string PublicIdOf(string creatorId) => $"{_options.Folder.Trim('/')}/{creatorId}";

        /// <summary>
        ///     The signature of a request: the parameters without file, cloud_name, resource_type and api_key, sorted by
        ///     name and written as name=value joined with &amp;, the secret added at the end, hashed with SHA-1.
        /// </summary>
        public static string Sign(IReadOnlyDictionary<string, string> parameters, string apiSecret)
        {
            var text = string.Join("&", parameters
                .Where(x => x.Key is not ("file" or "cloud_name" or "resource_type" or "api_key") &&
                            !string.IsNullOrEmpty(x.Value))
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => $"{x.Key}={x.Value}")) + apiSecret;
            return Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        }

        public async Task<StoredPhoto> Save(string creatorId, string contentType, byte[] data,
            CancellationToken cancellationToken = default)
        {
            var signed = new Dictionary<string, string>
            {
                // only pictures: Cloudinary refuses anything else even if it got past our own check
                ["allowed_formats"] = "jpg,png,webp",
                ["invalidate"] = "true",
                ["overwrite"] = "true",
                ["public_id"] = PublicIdOf(creatorId),
                ["timestamp"] = _timeProvider.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            };

            using var form = new MultipartFormDataContent();
            foreach (var (key, value) in signed) form.Add(new StringContent(value), key);
            form.Add(new StringContent(_options.ApiKey!), "api_key");
            form.Add(new StringContent(Sign(signed, _options.ApiSecret!)), "signature");
            var file = new ByteArrayContent(data);
            file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(file, "file", "photo");

            var answer = await Post<UploadDto>("image/upload", form, cancellationToken);
            if (string.IsNullOrEmpty(answer.PublicId) || answer.Version == 0)
                throw new PhotoStorageException("Cloudinary answered without the picture.");
            return new StoredPhoto(answer.Version.ToString(CultureInfo.InvariantCulture), answer.PublicId);
        }

        public async Task Remove(string creatorId, string? publicId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(publicId)) return;

            try
            {
                var signed = new Dictionary<string, string>
                {
                    ["invalidate"] = "true",
                    ["public_id"] = publicId,
                    ["timestamp"] = _timeProvider.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                };
                using var form = new MultipartFormDataContent();
                foreach (var (key, value) in signed) form.Add(new StringContent(value), key);
                form.Add(new StringContent(_options.ApiKey!), "api_key");
                form.Add(new StringContent(Sign(signed, _options.ApiSecret!)), "signature");
                await Post<object>("image/destroy", form, cancellationToken);
            }
            catch (PhotoStorageException e)
            {
                // the creator does not want the photo: it is gone from the profile either way
                _logger.LogWarning(e, "The photo {publicId} could not be deleted at Cloudinary", publicId);
            }
        }

        /// <summary>A square of the size, cut around the face if there is one, in the best format and quality for the browser</summary>
        public string? RedirectUrl(string publicId, string version, int size)
        {
            var bucket = PhotoSizes.Bucket(size);
            return $"{_options.DeliveryBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(_options.CloudName!)}/image/upload/" +
                   $"c_fill,g_face,w_{bucket},h_{bucket},f_auto,q_auto/v{Uri.EscapeDataString(version)}/{publicId}";
        }

        private async Task<T> Post<T>(string path, HttpContent content, CancellationToken cancellationToken)
        {
            var url = $"{_options.ApiBaseUrl.TrimEnd('/')}/v1_1/{Uri.EscapeDataString(_options.CloudName!)}/{path}";
            HttpResponseMessage response;
            try
            {
                response = await _client.PostAsync(url, content, cancellationToken);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                throw new PhotoStorageException("Cloudinary could not be reached.", e);
            }

            using (response)
            {
                var text = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    // Cloudinary says what is wrong in {"error":{"message":...}}; the secret is not part of it
                    string? message = null;
                    try
                    {
                        message = JsonSerializer.Deserialize<ErrorDto>(text, Json)?.Error?.Message;
                    }
                    catch (JsonException)
                    {
                        // not json
                    }

                    throw new PhotoStorageException(message ?? $"Cloudinary answered {(int) response.StatusCode}.");
                }

                try
                {
                    return JsonSerializer.Deserialize<T>(text, Json) ??
                           throw new PhotoStorageException("Cloudinary answered nothing.");
                }
                catch (JsonException e)
                {
                    throw new PhotoStorageException("Cloudinary answered something unreadable.", e);
                }
            }
        }

        private static readonly JsonSerializerOptions Json = new() {PropertyNameCaseInsensitive = true};

        private class UploadDto
        {
            [JsonPropertyName("public_id")] public string? PublicId { get; set; }

            [JsonPropertyName("version")] public long Version { get; set; }
        }

        private class ErrorDto
        {
            [JsonPropertyName("error")] public ErrorBody? Error { get; set; }
        }

        private class ErrorBody
        {
            [JsonPropertyName("message")] public string? Message { get; set; }
        }
    }
}
