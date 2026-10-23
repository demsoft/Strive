using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Strive.Core.Services.Payments;
using Strive.Core.Services.Videos;
using Strive.Infrastructure.Photos;
using Strive.Infrastructure.Videos;
using Xunit;

namespace Strive.Infrastructure.Tests.Videos
{
    public class CloudinaryVideoConverterTests
    {
        private const string Secret = "s3cr3t-value-of-the-account";

        private class Handler : HttpMessageHandler
        {
            public List<(HttpRequestMessage Request, string Body)> Calls { get; } = new();
            public Func<HttpRequestMessage, (HttpStatusCode Code, byte[] Body, long? Length)> Answer { get; set; } =
                _ => (HttpStatusCode.OK, Encoding.UTF8.GetBytes("{}"), null);
            public bool Throw { get; set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                if (Throw) throw new HttpRequestException("no network");
                Calls.Add((request, request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
                var (code, body, _) = Answer(request);
                return new HttpResponseMessage(code) {Content = new ByteArrayContent(body)};
            }
        }

        private class Time : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeSeconds(1700000000);
        }

        private static (CloudinaryVideoConverter converter, Handler handler) Create(bool configured = true)
        {
            var handler = new Handler();
            var options = Options.Create(new CloudinaryOptions
            {
                CloudName = configured ? "demo-cloud" : null, ApiKey = configured ? "123456" : null,
                ApiSecret = configured ? Secret : null,
            });
            return (new CloudinaryVideoConverter(new HttpClient(handler), options, Options.Create(new VideoOptions()),
                new Time()), handler);
        }

        private static byte[] Json(string text) => Encoding.UTF8.GetBytes(text);

        [Fact]
        public void IsConfigured_FollowsTheAccountSettings()
        {
            Assert.True(Create().converter.IsConfigured);
            Assert.False(Create(false).converter.IsConfigured);
        }

        [Fact]
        public void UploadTicket_IsASignedFormForOneVideo_ThatAsksForTheConversion()
        {
            var (converter, _) = Create();

            var ticket = converter.CreateUploadTicket("v1");

            Assert.Equal("https://api.cloudinary.com/v1_1/demo-cloud/video/upload", ticket.Url);
            Assert.Equal("gbedu/videos/v1", ticket.Fields["public_id"]);
            Assert.Equal("true", ticket.Fields["eager_async"]);
            Assert.Equal(CloudinaryVideoConverter.Eager, ticket.Fields["eager"]);
            Assert.Equal("123456", ticket.Fields["api_key"]);
            Assert.Equal("1700000000", ticket.Fields["timestamp"]);
            Assert.Contains("mp4", ticket.Fields["allowed_formats"]);
            // the signature covers everything but the key and itself, and the secret is never in the form
            var signed = ticket.Fields.Where(x => x.Key is not ("api_key" or "signature"))
                .ToDictionary(x => x.Key, x => x.Value);
            Assert.Equal(CloudinaryPhotoStorage.Sign(signed, Secret), ticket.Fields["signature"]);
            Assert.DoesNotContain(Secret, string.Join("", ticket.Fields.Values));
        }

        [Fact]
        public void TheConversionFitsAnyVideoIntoAnH264Mp4OfAtMost1280_AndMakesACover()
        {
            var parts = CloudinaryVideoConverter.Eager.Split('|');

            Assert.Equal(2, parts.Length);
            Assert.Contains("vc_h264", parts[0]);
            Assert.Contains("ac_aac", parts[0]);
            Assert.Contains("c_limit,w_1280,h_1280", parts[0]);
            Assert.EndsWith("/mp4", parts[0]);
            Assert.EndsWith("/jpg", parts[1]);
        }

        [Fact]
        public async Task GetStatus_FileArrivedAndConverted_GivesTheAddressesAndTheFacts()
        {
            var (converter, handler) = Create();
            handler.Answer = _ => (HttpStatusCode.OK, Json(
                "{\"duration\":61.5,\"bytes\":9000000,\"width\":1920,\"height\":1080,\"derived\":[" +
                "{\"format\":\"mp4\",\"bytes\":5000000,\"secure_url\":\"https://res.cloudinary.com/demo-cloud/video/upload/c_limit/v1/gbedu/videos/v1.mp4\"}," +
                "{\"format\":\"jpg\",\"bytes\":30000,\"secure_url\":\"https://res.cloudinary.com/demo-cloud/video/upload/so_1/v1/gbedu/videos/v1.jpg\"}]}"),
                null);

            var status = await converter.GetStatus("v1");

            Assert.True(status.Exists);
            Assert.Equal(61.5, status.DurationSeconds);
            Assert.Equal(1920, status.Width);
            Assert.EndsWith(".mp4", status.VideoUrl!.AbsolutePath);
            Assert.EndsWith(".jpg", status.PosterUrl!.AbsolutePath);
            var request = handler.Calls.Single().Request;
            Assert.Equal("https://api.cloudinary.com/v1_1/demo-cloud/resources/video/upload/gbedu/videos/v1",
                request.RequestUri!.ToString());
            Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
            Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("123456:" + Secret)),
                request.Headers.Authorization.Parameter);
        }

        [Fact]
        public async Task GetStatus_ArrivedButNotConvertedYet_HasNoAddresses()
        {
            var (converter, handler) = Create();
            handler.Answer = _ => (HttpStatusCode.OK, Json("{\"duration\":30,\"width\":720,\"height\":1280}"), null);

            var status = await converter.GetStatus("v1");

            Assert.True(status.Exists);
            Assert.Null(status.VideoUrl);
            Assert.Null(status.PosterUrl);
        }

        [Fact]
        public async Task GetStatus_OnlyTheVideoIsThere_TheCoverFollows()
        {
            var (converter, handler) = Create();
            handler.Answer = _ => (HttpStatusCode.OK, Json(
                "{\"duration\":30,\"derived\":[{\"format\":\"mp4\",\"secure_url\":\"https://res.cloudinary.com/x/v.mp4\"}]}"), null);

            var status = await converter.GetStatus("v1");

            Assert.NotNull(status.VideoUrl);
            Assert.Null(status.PosterUrl);
        }

        [Fact]
        public async Task GetStatus_NotThere_DoesNotExist()
        {
            var (converter, handler) = Create();
            handler.Answer = _ => (HttpStatusCode.NotFound, Json("{\"error\":{\"message\":\"Resource not found\"}}"), null);

            Assert.False((await converter.GetStatus("v1")).Exists);
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.TooManyRequests)]
        public async Task GetStatus_AnyOtherAnswer_IsAServiceException_NotMistakenForMissing(HttpStatusCode code)
        {
            var (converter, handler) = Create();
            handler.Answer = _ => (code, Json("{}"), null);

            await Assert.ThrowsAsync<VideoServiceException>(() => converter.GetStatus("v1"));
        }

        [Fact]
        public async Task GetStatus_NetworkDownOrNotJson_IsAServiceException()
        {
            var (down, downHandler) = Create();
            downHandler.Throw = true;
            await Assert.ThrowsAsync<VideoServiceException>(() => down.GetStatus("v1"));

            var (garbage, garbageHandler) = Create();
            garbageHandler.Answer = _ => (HttpStatusCode.OK, Json("<html>oops</html>"), null);
            await Assert.ThrowsAsync<VideoServiceException>(() => garbage.GetStatus("v1"));
        }

        [Fact]
        public async Task Open_GivesTheBytesAndTheirLength()
        {
            var (converter, handler) = Create();
            var bytes = Enumerable.Repeat((byte) 3, 4096).ToArray();
            handler.Answer = _ => (HttpStatusCode.OK, bytes, bytes.Length);

            using var file = await converter.Open(new Uri("https://res.cloudinary.com/demo-cloud/video/upload/v1/x.mp4"));

            Assert.Equal(bytes.Length, file.Length);
            using var copy = new MemoryStream();
            await file.Content.CopyToAsync(copy);
            Assert.Equal(bytes, copy.ToArray());
        }

        [Theory]
        [InlineData("http://res.cloudinary.com/demo-cloud/x.mp4")] // not https
        [InlineData("https://evil.example.com/x.mp4")]
        [InlineData("https://res.cloudinary.com.evil.example.com/x.mp4")]
        [InlineData("https://localhost/x.mp4")]
        public async Task Open_OnlyFetchesFromCloudinary(string url)
        {
            var (converter, handler) = Create();

            await Assert.ThrowsAsync<VideoServiceException>(() => converter.Open(new Uri(url)));

            Assert.Empty(handler.Calls);
        }

        [Theory]
        [InlineData(HttpStatusCode.Locked)]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task Open_NotThereOrStillBeingMade_IsAServiceException(HttpStatusCode code)
        {
            var (converter, handler) = Create();
            handler.Answer = _ => (code, Array.Empty<byte>(), null);

            var error = await Assert.ThrowsAsync<VideoServiceException>(() =>
                converter.Open(new Uri("https://res.cloudinary.com/demo-cloud/video/upload/v1/x.mp4")));

            Assert.Contains(((int) code).ToString(), error.Message);
        }

        [Fact]
        public async Task Delete_DestroysTheVideoWithASignature()
        {
            var (converter, handler) = Create();

            await converter.Delete("v1");

            var (request, body) = Assert.Single(handler.Calls);
            Assert.Equal("https://api.cloudinary.com/v1_1/demo-cloud/video/destroy", request.RequestUri!.ToString());
            Assert.Contains("gbedu/videos/v1", body);
            Assert.DoesNotContain(Secret, body);
        }

        [Fact]
        public async Task Delete_Refused_OrNetworkDown_IsAServiceException()
        {
            var (refused, refusedHandler) = Create();
            refusedHandler.Answer = _ => (HttpStatusCode.Unauthorized, Json("{}"), null);
            await Assert.ThrowsAsync<VideoServiceException>(() => refused.Delete("v1"));

            var (down, downHandler) = Create();
            downHandler.Throw = true;
            await Assert.ThrowsAsync<VideoServiceException>(() => down.Delete("v1"));
        }
    }
}
