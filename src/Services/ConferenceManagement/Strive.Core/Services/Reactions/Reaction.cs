using System.Linq;using System.Collections.Generic;

namespace Strive.Core.Services.Reactions
{
    public static class Reaction
    {
        public static readonly IReadOnlyList<string> AllowedEmojis = new[] {"👍", "👏", "❤️", "😂", "😮", "🎉"};

        public static bool IsAllowed(string emoji)
        {
            return AllowedEmojis.Contains(emoji);
        }
    }
}
