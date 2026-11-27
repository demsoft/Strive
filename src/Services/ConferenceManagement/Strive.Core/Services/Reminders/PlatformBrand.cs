using Strive.Core.Services.Payments;
using Strive.Core.Services.Payments.Sessions;

namespace Strive.Core.Services.Reminders
{
    /// <summary>
    ///     The brand a mail of the creator platform speaks for: its name (shown as the sender) and the address its links
    ///     start with. Matches and approved football organizers are Arena, everything else is Gbedu.
    /// </summary>
    public record PlatformBrand(string Name, string LinkBaseUrl, bool IsArena)
    {
        public static PlatformBrand For(RemindersOptions options, bool arena) => arena
            ? new PlatformBrand("Arena",
                string.IsNullOrWhiteSpace(options.ArenaLinkBaseUrl) ? options.LinkBaseUrl : options.ArenaLinkBaseUrl, true)
            : new PlatformBrand("Gbedu", options.LinkBaseUrl, false);

        public static PlatformBrand Of(RemindersOptions options, PaidSession session) =>
            For(options, session.Match != null);

        public static PlatformBrand Of(RemindersOptions options, CreatorProfile? creator) =>
            For(options, creator?.OrganizerStatus == OrganizerStatus.Approved);

        /// <summary>The address of a page of the brand, like "s/abc"</summary>
        public string Link(string path) => $"{LinkBaseUrl.TrimEnd('/')}/{path}";
    }
}
