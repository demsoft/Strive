import { DateTime } from 'luxon';
import { expect, test } from 'vitest';
import { DiscoverSession } from 'src/services/api/sessions';
import { competitionsOf, filterMatches, sortMatches } from './matchFilters';

const now = DateTime.fromISO('2026-11-14T10:00:00');

const match = (
   id: string,
   over: Partial<DiscoverSession> = {},
   home = 'Lagos FC',
   away = 'Abuja United',
   competition: string | null = 'State Cup',
): DiscoverSession => ({
   sessionId: id,
   title: `${home} vs ${away}`,
   category: 'match',
   priceMinor: 300000,
   currency: 'NGN',
   startsAt: '2026-11-14T16:00:00',
   creatorName: 'Lagos League',
   isHost: false,
   hasTicket: false,
   durationMinutes: 120,
   isLive: false,
   match: { homeTeam: home, awayTeam: away, competition },
   ...over,
});

const ids = (list: DiscoverSession[]) => list.map((x) => x.sessionId);

test('competitions: the ones with most matches first, matches without one are left out', () => {
   const list = [
      match('a'),
      match('b', {}, 'A', 'B', 'Schools League'),
      match('c', {}, 'C', 'D', 'Schools League'),
      match('d', {}, 'E', 'F', null),
   ];

   expect(competitionsOf(list)).toEqual(['Schools League', 'State Cup']);
});

test('words are found in the teams, the competition or the organizer, without capitals or accents', () => {
   const list = [
      match('a'),
      match('b', { creatorName: 'Ikorodu Académie' }, 'Ikorodu Stars', 'Ibadan Lions', 'Schools League'),
   ];

   expect(ids(filterMatches(list, 'lagos cup', null, 'all', now))).toEqual(['a']);
   expect(ids(filterMatches(list, 'academie', null, 'all', now))).toEqual(['b']);
   expect(ids(filterMatches(list, 'zzz', null, 'all', now))).toEqual([]);
});

test('a competition narrows the list', () => {
   const list = [match('a'), match('b', {}, 'A', 'B', 'Schools League')];

   expect(ids(filterMatches(list, '', 'Schools League', 'all', now))).toEqual(['b']);
});

test('when: live now, today, this week', () => {
   const list = [
      match('live', { isLive: true, startsAt: '2026-11-14T08:00:00' }),
      match('today', { startsAt: '2026-11-14T20:00:00' }),
      match('tomorrow', { startsAt: '2026-11-15T16:00:00' }),
      match('later', { startsAt: '2026-12-20T16:00:00' }),
      match('undated', { startsAt: null }),
   ];

   expect(ids(filterMatches(list, '', null, 'live', now))).toEqual(['live']);
   expect(ids(filterMatches(list, '', null, 'today', now))).toEqual(['live', 'today']);
   expect(ids(filterMatches(list, '', null, 'week', now))).toEqual(['live', 'today', 'tomorrow']);
   expect(ids(filterMatches(list, '', null, 'all', now))).toHaveLength(5);
});

test('sorting: live first, then by kick-off, undated last', () => {
   const list = [
      match('undated', { startsAt: null }),
      match('later', { startsAt: '2026-12-20T16:00:00' }),
      match('live', { isLive: true }),
      match('soon', { startsAt: '2026-11-14T20:00:00' }),
   ];

   expect(ids(sortMatches(list))).toEqual(['live', 'soon', 'later', 'undated']);
});
