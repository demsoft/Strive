using MediatR;
using Strive.Core.Interfaces;

namespace Strive.Core.Services.Recording.Requests
{
    /// <summary>
    ///     The recorder service reports what happens with a recording
    /// </summary>
    public record RecorderReportRequest(string RecordingId, RecorderReport Report) : IRequest<SuccessOrError<Unit>>;

    public enum RecorderEvent
    {
        /// <summary>
        ///     The recorder joined the conference and started recording
        /// </summary>
        Started,

        /// <summary>
        ///     The file is complete and uploaded
        /// </summary>
        Finished,
        Failed,
    }

    public record RecorderReport(RecorderEvent Event)
    {
        public string? StorageKey { get; init; }

        public long? SizeBytes { get; init; }

        public double? DurationSeconds { get; init; }

        public string? Reason { get; init; }
    }
}
