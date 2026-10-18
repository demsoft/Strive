namespace Strive.Core.Services.Latency
{
    /// <summary>
    ///     What a participant measured about their own connection. With the numbers of two participants the delay of
    ///     the sound from one to the other can be estimated (see the web app).
    /// </summary>
    /// <param name="RttMs">Round trip time to the media server</param>
    /// <param name="JitterBufferMs">How long incoming sound waits in the buffer of the browser</param>
    /// <param name="OutputMs">Delay of the sound system of the computer until the sound is heard</param>
    public record LatencyReport(double RttMs, double JitterBufferMs, double OutputMs)
    {
        public const double MaxMs = 5000;

        public bool IsValid()
        {
            return IsPlausible(RttMs) && IsPlausible(JitterBufferMs) && IsPlausible(OutputMs);
        }

        private static bool IsPlausible(double value)
        {
            return double.IsFinite(value) && value >= 0 && value <= MaxMs;
        }

        /// <summary>The sum, to see if a new report differs enough from the last to be worth telling the others</summary>
        public double Total => RttMs + JitterBufferMs + OutputMs;
    }
}
