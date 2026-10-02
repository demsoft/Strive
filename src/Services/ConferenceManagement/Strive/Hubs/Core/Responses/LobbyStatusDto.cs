namespace Strive.Hubs.Core.Responses
{
    public record LobbyStatusDto(string Status);

    public static class LobbyStatus
    {
        /// <summary>
        ///     The participant has to wait until a moderator admits him
        /// </summary>
        public const string Waiting = "waiting";

        /// <summary>
        ///     The participant was admitted and joined the conference
        /// </summary>
        public const string Admitted = "admitted";

        public const string Denied = "denied";
    }
}
