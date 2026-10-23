using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Strive.Core.Services.Payments;
using Strive.Infrastructure.Photos;
using Xunit;

namespace Strive.Infrastructure.Tests.Photos
{
    public class CloudinaryPhotoStorageTests
    {
        private const string Secret = "s3cr3t-value-of-the-account";

        private class Handler : HttpMessageHandler
        {
            public List<(HttpRequestMessage Request, string Body)> Calls { get; } = new();
            public HttpStatusCode Code { get; set; } = HttpStatusCode.OK;
            public string Answer { get; set; } = "{\"public_id\":\"gbedu/creators/c1\",\"version\":1700000001}";
            public bool Throw { get; set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                if (Throw) throw new HttpRequestException("no network");
                Calls.Add((request, await request.Content!.ReadAsStringAsync(cancellationToken)));
                return new HttpResponseMessage(Code) {Content = new StringContent(Answer, Encoding.UTF8, "application/json")};
            }
        }

        private class Time : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeSeconds(1700000000);
        }

        private static (CloudinaryPhotoStorage storage, Handler handler) Create(bool configured = true)
        {
            var handler = new Handler();
            var options = Options.Create(new CloudinaryOptions
            {
                CloudName = configured ? "demo-cloud" : null,
                ApiKey = configured ? "123456" : null,
                ApiSecret = configured ? Secret : null,
            });
            return (new CloudinaryPhotoStorage(new HttpClient(handler), options, new Time(),
                NullLogger<CloudinaryPhotoStorage>.Instance), handler);
        }

        private static string Field(string body, string name)
        {
            // multipart: ... name="x"\r\n\r\nvalue\r\n--boundary
            // .NET writes name=public_id, other writers name="public_id"
            var match = System.Text.RegularExpressions.Regex.Match(body, $"name=\"?{name}\"?(?=[;\r\n])");
            if (!match.Success) return "";
            var start = match.Index;
            var valueStart = body.IndexOf("\r\n\r\n", start, StringComparison.Ordinal) + 4;
            return body[valueStart..body.IndexOf("\r\n", valueStart, StringComparison.Ordinal)];
        }

        [Fact]
        public void Sign_SortsTheParameters_LeavesOutWhatIsNotSigned_AndAddsTheSecret()
        {
            var signature = CloudinaryPhotoStorage.Sign(new Dictionary<string, string>
            {
                ["timestamp"] = "1315060510",
                ["public_id"] = "sample_image",
                ["file"] = "data",
                ["api_key"] = "key",
                ["resource_type"] = "image",
                ["cloud_name"] = "demo",
                ["empty"] = "",
                ["eager"] = "w_400,h_300,c_pad|w_260,h_200,c_crop",
            }, "abcd");

            var expected = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(
                "eager=w_400,h_300,c_pad|w_260,h_200,c_crop&public_id=sample_image&timestamp=1315060510abcd"))).ToLowerInvariant();
            Assert.Equal(expected, signature);
        }

        [Fact]
        public async Task Save_UploadsASignedPictureToTheAccountAndNamesItAfterTheCreator()
        {
            var (storage, handler) = Create();

            var stored = await storage.Save("c1", "image/jpeg", new byte[] {1, 2, 3, 4});

            var (request, body) = Assert.Single(handler.Calls);
            Assert.Equal("https://api.cloudinary.com/v1_1/demo-cloud/image/upload", request.RequestUri!.ToString());
            Assert.Equal("gbedu/creators/c1", Field(body, "public_id"));
            Assert.Equal("true", Field(body, "overwrite"));
            Assert.Equal("true", Field(body, "invalidate"));
            Assert.Equal("jpg,png,webp", Field(body, "allowed_formats"));
            Assert.Equal("1700000000", Field(body, "timestamp"));
            Assert.Equal("123456", Field(body, "api_key"));
            Assert.Contains("name=file", body.Replace("\"", ""));
            // the signature is the one of the signed fields, made with the secret
            var signed = new Dictionary<string, string>
            {
                ["allowed_formats"] = "jpg,png,webp", ["invalidate"] = "true", ["overwrite"] = "true",
                ["public_id"] = "gbedu/creators/c1", ["timestamp"] = "1700000000",
            };
            Assert.Equal(CloudinaryPhotoStorage.Sign(signed, Secret), Field(body, "signature"));
            Assert.DoesNotContain(Secret, body);
            Assert.Equal(new StoredPhoto("1700000001", "gbedu/creators/c1"), stored);
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest, "{\"error\":{\"message\":\"Invalid image file\"}}", "Invalid image file")]
        [InlineData(HttpStatusCode.Unauthorized, "{\"error\":{\"message\":\"Invalid Signature\"}}", "Invalid Signature")]
        [InlineData(HttpStatusCode.InternalServerError, "oops, not json", "500")]
        public async Task Save_CloudinaryRefuses_SaysWhyButNeverTheSecret(HttpStatusCode code, string answer, string expected)
        {
            var (storage, handler) = Create();
            handler.Code = code;
            handler.Answer = answer;

            var error = await Assert.ThrowsAsync<PhotoStorageException>(() =>
                storage.Save("c1", "image/jpeg", new byte[] {1}));

            Assert.Contains(expected, error.Message);
            Assert.DoesNotContain(Secret, error.Message);
        }

        [Fact]
        public async Task Save_NetworkDown_OrAnswerWithoutThePicture_IsAStorageException()
        {
            var (down, downHandler) = Create();
            downHandler.Throw = true;
            await Assert.ThrowsAsync<PhotoStorageException>(() => down.Save("c1", "image/jpeg", new byte[] {1}));

            var (empty, emptyHandler) = Create();
            emptyHandler.Answer = "{}";
            await Assert.ThrowsAsync<PhotoStorageException>(() => empty.Save("c1", "image/jpeg", new byte[] {1}));
        }

        [Fact]
        public async Task Remove_DeletesThePictureWithASignature()
        {
            var (storage, handler) = Create();

            await storage.Remove("c1", "gbedu/creators/c1");

            var (request, body) = Assert.Single(handler.Calls);
            Assert.Equal("https://api.cloudinary.com/v1_1/demo-cloud/image/destroy", request.RequestUri!.ToString());
            Assert.Equal("gbedu/creators/c1", Field(body, "public_id"));
            Assert.Equal("true", Field(body, "invalidate"));
            var signed = new Dictionary<string, string>
                {["invalidate"] = "true", ["public_id"] = "gbedu/creators/c1", ["timestamp"] = "1700000000"};
            Assert.Equal(CloudinaryPhotoStorage.Sign(signed, Secret), Field(body, "signature"));
            Assert.DoesNotContain(Secret, body);
        }

        [Fact]
        public async Task Remove_Fails_IsNotThrown_AndNothingToRemoveIsNotAsked()
        {
            var (storage, handler) = Create();
            handler.Code = HttpStatusCode.InternalServerError;

            await storage.Remove("c1", "gbedu/creators/c1");
            handler.Throw = true;
            await storage.Remove("c1", "gbedu/creators/c1");
            var before = handler.Calls.Count;
            await storage.Remove("c1", null);

            Assert.Equal(before, handler.Calls.Count);
        }

        [Theory]
        [InlineData(20, 64)]
        [InlineData(64, 64)]
        [InlineData(65, 128)]
        [InlineData(200, 256)]
        [InlineData(400, 400)]
        [InlineData(5000, 400)]
        public void RedirectUrl_IsASquareCutAroundTheFaceInTheBestFormat_AtTheNextSize(int asked, int bucket)
        {
            var (storage, _) = Create();

            var url = storage.RedirectUrl("gbedu/creators/c1", "1700000001", asked);

            Assert.Equal(
                $"https://res.cloudinary.com/demo-cloud/image/upload/c_fill,g_face,w_{bucket},h_{bucket},f_auto,q_auto/v1700000001/gbedu/creators/c1",
                url);
        }

        [Fact]
        public void IsConfigured_NeedsTheNameTheKeyAndTheSecret()
        {
            Assert.True(new CloudinaryOptions {CloudName = "a", ApiKey = "b", ApiSecret = "c"}.IsConfigured);
            Assert.False(new CloudinaryOptions {CloudName = "a", ApiKey = "b"}.IsConfigured);
            Assert.False(new CloudinaryOptions {CloudName = "a", ApiSecret = "c"}.IsConfigured);
            Assert.False(new CloudinaryOptions().IsConfigured);
        }
    }
}
