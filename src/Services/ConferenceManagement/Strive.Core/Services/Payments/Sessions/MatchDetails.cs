using System;
using System.Collections.Generic;
using System.Linq;

namespace Strive.Core.Services.Payments.Sessions
{
    /// <summary>The football match a session shows (Arena)</summary>
    public record MatchDetails(string HomeTeam, string AwayTeam, string? Competition);

    /// <summary>Why the organizer may stream a match</summary>
    public static class RightsBasis
    {
        /// <summary>The organizer plays or organizes the match (their own league, school or club)</summary>
        public const string Own = "own";

        /// <summary>The organizer holds a licence of whoever owns the rights</summary>
        public const string Licensed = "licensed";

        /// <summary>The organizer has the written permission of whoever owns the rights</summary>
        public const string Permission = "permission";

        public static readonly IReadOnlyList<string> All = new[] {Own, Licensed, Permission};

        /// <summary>For these the organizer says who gave the licence or the permission</summary>
        public static bool NeedsNote(string basis) => basis != Own;
    }

    /// <summary>What the organizer declared about the rights to stream a match, kept with the session</summary>
    public record SessionRights(string Basis, string? Note, DateTimeOffset DeclaredAt, string DeclaredByUserId);

    /// <summary>The match and the declaration as the organizer sends them</summary>
    public record MatchInput(string HomeTeam, string AwayTeam, string? Competition, string RightsBasis,
        string? RightsNote, bool RightsAccepted);

    public static class MatchRules
    {
        public const int MinTeamLength = 2;
        public const int MaxTeamLength = 40;
        public const int MaxCompetitionLength = 60;
        public const int MinNoteLength = 3;
        public const int MaxNoteLength = 300;

        /// <summary>Checks the input. Returns the match and the basis of the rights, or the message to show.</summary>
        public static (MatchDetails? Match, string? Note, string? Error) Validate(MatchInput input)
        {
            var home = input.HomeTeam?.Trim() ?? "";
            var away = input.AwayTeam?.Trim() ?? "";
            if (home.Length is < MinTeamLength or > MaxTeamLength || away.Length is < MinTeamLength or > MaxTeamLength)
                return (null, null, $"Each team needs {MinTeamLength} to {MaxTeamLength} characters.");
            if (string.Equals(home, away, StringComparison.OrdinalIgnoreCase))
                return (null, null, "A team cannot play itself.");

            var competition = string.IsNullOrWhiteSpace(input.Competition) ? null : input.Competition.Trim();
            if (competition is {Length: > MaxCompetitionLength})
                return (null, null, $"The competition can have {MaxCompetitionLength} characters at most.");

            if (!input.RightsAccepted)
                return (null, null, "Confirm that you have the right to stream this match.");
            if (!RightsBasis.All.Contains(input.RightsBasis))
                return (null, null, "Say why you may stream this match.");

            var note = string.IsNullOrWhiteSpace(input.RightsNote) ? null : input.RightsNote.Trim();
            if (note is {Length: > MaxNoteLength})
                return (null, null, $"The note can have {MaxNoteLength} characters at most.");
            if (RightsBasis.NeedsNote(input.RightsBasis) && (note == null || note.Length < MinNoteLength))
                return (null, null, "Say who gave you the licence or the permission.");

            return (new MatchDetails(home, away, competition), note, null);
        }
    }
}
