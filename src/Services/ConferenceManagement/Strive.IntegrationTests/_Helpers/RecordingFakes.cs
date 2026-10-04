using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Strive.Core.Services.Recording.Gateways;

namespace Strive.IntegrationTests._Helpers
{
    /// <summary>
    ///     Stands in for the recorder service: remembers what it was asked to do
    /// </summary>
    public class FakeRecorderClient : IRecorderClient
    {
        public ConcurrentQueue<RecorderStartCommand> Started { get; } = new();

        public ConcurrentQueue<string> Stopped { get; } = new();

        public bool FailOnStart { get; set; }

        public Task Start(RecorderStartCommand command, CancellationToken cancellationToken = default)
        {
            if (FailOnStart) throw new InvalidOperationException("The recorder is down.");

            Started.Enqueue(command);
            return Task.CompletedTask;
        }

        public Task Stop(string recordingId, CancellationToken cancellationToken = default)
        {
            Stopped.Enqueue(recordingId);
            return Task.CompletedTask;
        }
    }

    public class FakeRecordingStorage : IRecordingStorage
    {
        public ConcurrentBag<string> Deleted { get; } = new();

        public Uri GetPlaybackUrl(string storageKey, TimeSpan validFor)
        {
            return new Uri($"https://storage.test/{storageKey}?expires={(int) validFor.TotalSeconds}&sig=fake");
        }

        public Task Delete(string storageKey, CancellationToken cancellationToken = default)
        {
            Deleted.Add(storageKey);
            return Task.CompletedTask;
        }

        public Task<bool> Exists(string storageKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }
}
