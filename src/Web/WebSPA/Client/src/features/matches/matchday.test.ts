import { DateTime } from 'luxon';
import { expect, test } from 'vitest';
import { DiscoverSession } from 'src/services/api/sessions';
import { byCompetition, matchdayOf } from './matchday';

const now = DateTime.fromISO('2026-11-14T10:00:00');
const match = (
   id: string,
   over: Partial<DiscoverSession> = {},
   competition: string | null = 'State Cup',
): DiscoverSession => ({
   sessionId: id,
   title: id,
   category: 'match',
   priceMinor: 300000,
   currency: 'NGN',
   startsAt: '2026-11-14T16:00:00',
   creatorName: 'Lagos League',
   isHost: false,
   hasTicket: false,
   durationMinutes: 120,
   isLive: false,
   match: { homeTeam: 'A', awayTeam: 'B', competition },
   ...over,
});
const ids = (list: DiscoverSession[]) => list.map((x) => x.sessionId);

test('matches are sorted into live, today, this week and later, each in one group', () => {
   const day = matchdayOf(
      [
         match('later', { startsAt: '2026-12-20T16:00:00' }),
         match('undated', { startsAt: null }),
         match('tomorrow', { startsAt: '2026-11-15T16:00:00' }),
         match('today', { startsAt: '2026-11-14T20:00:00' }),
         match('live', { isLive: true, startsAt: '2026-11-14T08:00:00' }),
      ],
      now,
   );

   expect(ids(day.live)).toEqual(['live']);
   expect(ids(day.today)).toEqual(['today']);
   expect(ids(day.week)).toEqual(['tomorrow']);
   expect(ids(day.later)).toEqual(['later', 'undated']);
});

test('the featured match is a live one, else the next to kick off', () => {
   const live = matchdayOf([match('today'), match('live', { isLive: true })], now);
   expect(live.featured?.sessionId).toBe('live');

   const next = matchdayOf([match('tomorrow', { startsAt: '2026-11-15T16:00:00' }), match('today')], now);
   expect(next.featured?.sessionId).toBe('today');

   expect(matchdayOf([], now).featured).toBeNull();
});

test('by competition: the biggest first, matches without one last', () => {
   const groups = byCompetition([
      match('a'),
      match('b', {}, 'Schools League'),
      match('c', {}, 'Schools League'),
      match('d', {}, null),
   ]);

   expect(groups.map((g) => g.competition)).toEqual(['Schools League', 'State Cup', null]);
   expect(ids(groups[0].matches)).toEqual(['b', 'c']);
});
