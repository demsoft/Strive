namespace Strive.Core.Services.Reactions
{
    /// <summary>
    ///     Limits how often a participant can send reactions. State is kept in memory as reactions are ephemeral.
    /// </summary>
    public interface IReactionRateLimiter
    {
        /// <returns>Returns true if the participant may send a reaction now (and counts it), false if the limit is exceeded</returns>
        bool TryAcquire(Participant participant);

        void Remove(Participant participant);
    }
}
