import { DateTime } from 'luxon';
import { DiscoverSession } from 'src/services/api/sessions';
import { filterMatches, sortMatches } from './matchFilters';

export type Matchday = {
   live: DiscoverSession[];
   /** kicks off later today */
   today: DiscoverSession[];
   /** the next days */
   week: DiscoverSession[];
   /** further away, or without a date */
   later: DiscoverSession[];
   /** the match to show big: a live one, else the next to kick off */
   featured: DiscoverSession | null;
};

/** Sorts the matches into what is on now, today, this week and later (each match is in one group only). */
export function matchdayOf(matches: DiscoverSession[], now: DateTime = DateTime.now()): Matchday {
   const sorted = sortMatches(matches);
   const live = sorted.filter((x) => x.isLive);
   const rest = sorted.filter((x) => !x.isLive);
   const today = filterMatches(rest, '', null, 'today', now);
   const week = filterMatches(rest, '', null, 'week', now).filter((x) => !today.includes(x));
   const later = rest.filter((x) => !today.includes(x) && !week.includes(x));
   return { live, today, week, later, featured: live[0] ?? today[0] ?? week[0] ?? later[0] ?? null };
}

/** The matches grouped by competition, the competition with the most matches first (no competition: last) */
export function byCompetition(
   matches: DiscoverSession[],
): { competition: string | null; matches: DiscoverSession[] }[] {
   const groups = new Map<string | null, DiscoverSession[]>();
   for (const match of matches) {
      const key = match.match?.competition?.trim() || null;
      groups.set(key, [...(groups.get(key) ?? []), match]);
   }
   return [...groups.entries()]
      .sort((a, b) => (a[0] === null ? 1 : b[0] === null ? -1 : b[1].length - a[1].length || a[0].localeCompare(b[0])))
      .map(([competition, list]) => ({ competition, matches: list }));
}
