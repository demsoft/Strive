import { DateTime } from 'luxon';
import { DiscoverSession } from 'src/services/api/sessions';

/** When a match is: on now, today, in the next seven days, later, or without a date yet */
export type WhenFilter = 'all' | 'live' | 'today' | 'week';

const normalize = (text: string) => text.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();

/** The competitions of the matches, the ones with most matches first (a match with none is left out) */
export function competitionsOf(matches: DiscoverSession[]): string[] {
   const counts = new Map<string, number>();
   for (const match of matches) {
      const name = match.match?.competition?.trim();
      if (name) counts.set(name, (counts.get(name) ?? 0) + 1);
   }
   return [...counts.entries()].sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0])).map(([name]) => name);
}

/**
 * The matches that fit what the person chose: the words (the teams, the competition or the organizer, every word has
 * to be found, no capitals, no accents), the competition (none: all) and when it is.
 */
export function filterMatches(
   matches: DiscoverSession[],
   query: string,
   competition: string | null,
   when: WhenFilter,
   now: DateTime = DateTime.now(),
): DiscoverSession[] {
   const words = normalize(query).split(/\s+/).filter(Boolean);
   const endOfToday = now.endOf('day');
   const endOfWeek = now.plus({ days: 7 }).endOf('day');

   return matches.filter((session) => {
      if (competition && session.match?.competition !== competition) return false;

      if (when === 'live' && !session.isLive) return false;
      if (when === 'today' || when === 'week') {
         // live now counts as today; a match without a date has no day to be on
         if (!session.isLive) {
            if (!session.startsAt) return false;
            const start = DateTime.fromISO(session.startsAt);
            if (start > (when === 'today' ? endOfToday : endOfWeek)) return false;
         }
      }

      const haystack = normalize(
         `${session.match?.homeTeam ?? ''} ${session.match?.awayTeam ?? ''} ${session.match?.competition ?? ''} ${
            session.title
         } ${session.creatorName}`,
      );
      return words.every((word) => haystack.includes(word));
   });
}

/** Live matches first, then by kick-off; the ones without a date last */
export function sortMatches(matches: DiscoverSession[]): DiscoverSession[] {
   return [...matches].sort(
      (a, b) => Number(b.isLive) - Number(a.isLive) || (a.startsAt ?? '9999').localeCompare(b.startsAt ?? '9999'),
   );
}
