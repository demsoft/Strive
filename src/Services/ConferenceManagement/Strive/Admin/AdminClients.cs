using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Strive.Core.Services.Recording;
using Strive.Infrastructure.Recording;

namespace Strive.Admin
{
    public record Fetched<T>(T? Value, string? Error, int LatencyMs) where T : class
    {
        public bool Ok => Value != null;
    }

    public interface IAdminSourceClient
    {
        Task<Fetched<SfuStats>?> GetSfuAsync(CancellationToken cancellationToken);
        Task<Fetched<AccountStats>?> GetAccountsAsync(CancellationToken cancellationToken);
        Task<Fetched<RecorderStats>?> GetRecorderAsync(CancellationToken cancellationToken);
    }

    /// <summary>Reads the numbers of the other services over their internal addresses. Every call has a short timeout.</summary>
    public class HttpAdminSourceClient : IAdminSourceClient
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(4);

        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _http;
        private readonly AdminOptions _options;
        private readonly RecorderOptions _recorder;
        private readonly RecordingOptions _recording;

        public HttpAdminSourceClient(HttpClient http, IOptions<AdminOptions> options,
            IOptions<RecordingOptions> recording, IOptions<RecorderOptions> recorder)
        {
            _http = http;
            _options = options.Value;
            _recording = recording.Value;
            _recorder = recorder.Value;
        }

        public Task<Fetched<SfuStats>?> GetSfuAsync(CancellationToken cancellationToken) =>
            GetAsync<SfuStats>(_options.SfuUrl, "/admin/stats", "X-Api-Key", _options.ApiKey, cancellationToken);

        public Task<Fetched<AccountStats>?> GetAccountsAsync(CancellationToken cancellationToken) =>
            GetAsync<AccountStats>(_options.IdentityUrl, "/api/v1/admin/stats", "X-Api-Key", _options.ApiKey,
                cancellationToken);

        public Task<Fetched<RecorderStats>?> GetRecorderAsync(CancellationToken cancellationToken)
        {
            if (!_recording.Enabled) return Task.FromResult<Fetched<RecorderStats>?>(null);

            var url = !string.IsNullOrWhiteSpace(_options.RecorderUrl)
                ? _options.RecorderUrl
                : _recorder.BaseUrl;
            return GetAsync<RecorderStats>(url, "/stats", "X-Recorder-Secret", _recorder.SharedSecret,
                cancellationToken);
        }

        private async Task<Fetched<T>?> GetAsync<T>(string? baseUrl, string path, string keyHeader, string? key,
            CancellationToken cancellationToken) where T : class
        {
            // not configured: not part of this installation
            if (string.IsNullOrWhiteSpace(baseUrl)) return null;

            var watch = Stopwatch.StartNew();
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(Timeout);

                using var request = new HttpRequestMessage(HttpMethod.Get, baseUrl.TrimEnd('/') + path);
                if (!string.IsNullOrEmpty(key)) request.Headers.Add(keyHeader, key);

                using var response = await _http.SendAsync(request, timeout.Token);
                if (!response.IsSuccessStatusCode)
                    return new Fetched<T>(null, $"answered {(int) response.StatusCode}", (int) watch.ElapsedMilliseconds);

                var value = await response.Content.ReadFromJsonAsync<T>(Json, timeout.Token);
                return new Fetched<T>(value, value == null ? "empty answer" : null, (int) watch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return new Fetched<T>(null, "no answer within 4 seconds", (int) watch.ElapsedMilliseconds);
            }
            catch (Exception e) when (e is HttpRequestException or JsonException)
            {
                return new Fetched<T>(null, e.Message, (int) watch.ElapsedMilliseconds);
            }
        }
    }
}
