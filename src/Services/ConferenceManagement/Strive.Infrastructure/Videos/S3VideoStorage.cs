using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Strive.Core.Services.Videos;

namespace Strive.Infrastructure.Videos
{
    /// <summary>The converted videos in an S3 compatible store (Cloudflare R2); played through temporary links.</summary>
    public class S3VideoStorage : IVideoStorage, IDisposable
    {
        private readonly VideoStorageOptions _options;
        private readonly Lazy<AmazonS3Client> _client;

        public S3VideoStorage(IOptions<VideoStorageOptions> options)
        {
            _options = options.Value;
            // created on first use: the storage may not be set up
            _client = new Lazy<AmazonS3Client>(() => new AmazonS3Client(_options.AccessKeyId, _options.SecretAccessKey,
                new AmazonS3Config
                {
                    ServiceURL = _options.ServiceUrl,
                    AuthenticationRegion = _options.Region,
                    ForcePathStyle = _options.ForcePathStyle,
                    // R2 does not know the checksums the newer SDK adds on its own
                    RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                    ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
                }));
        }

        public bool IsConfigured => _options.IsConfigured;

        public async Task Put(string key, Stream content, long length, string contentType,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var request = new PutObjectRequest
                {
                    BucketName = _options.Bucket,
                    Key = key,
                    InputStream = content,
                    ContentType = contentType,
                    // over https the body does not need its own signature (R2 does not take the streaming kind)
                    DisablePayloadSigning = true,
                    UseChunkEncoding = false,
                };
                request.Headers.ContentLength = length;
                await _client.Value.PutObjectAsync(request, cancellationToken);
            }
            catch (Exception e) when (e is AmazonServiceException or AmazonClientException or IOException)
            {
                throw new VideoServiceException("The video could not be stored.", e);
            }
        }

        public Uri GetPlaybackUrl(string key, TimeSpan validFor, string contentType)
        {
            var url = _client.Value.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = _options.Bucket,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.Add(validFor),
                ResponseHeaderOverrides = new ResponseHeaderOverrides {ContentType = contentType, ContentDisposition = "inline"},
            });
            return new Uri(url);
        }

        public async Task Delete(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                // deleting a key that is not there is no error in S3
                await _client.Value.DeleteObjectAsync(_options.Bucket, key, cancellationToken);
            }
            catch (Exception e) when (e is AmazonServiceException or AmazonClientException)
            {
                throw new VideoServiceException("The video could not be deleted.", e);
            }
        }

        public void Dispose()
        {
            if (_client.IsValueCreated) _client.Value.Dispose();
        }
    }
}
