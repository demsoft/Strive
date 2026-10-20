import { DateTime } from 'luxon';
import { describe, expect, test } from 'vitest';
import { Ticket } from 'src/services/api/sessions';
import { splitTickets } from './MyTicketsPage';
import { initialsOf } from 'src/features/gbedu/ui/HostAvatar';
import { categoryStyle } from 'src/features/gbedu/ui/categories';

const NOW = DateTime.fromISO('2026-10-19T12:00:00Z');
const ticket = (id: string, startsAt: string | null, status: Ticket['status'] = 'published'): Ticket => ({
   sessionId: id,
   conferenceId: 'room' + id,
   title: id,
   category: 'music',
   startsAt,
   creatorName: 'Ada',
   grantedAt: '',
   status,
   refunded: false,
});

describe('splitTickets', () => {
   test('a session that has not started, or has no time, is upcoming', () => {
      const { upcoming, past } = splitTickets([ticket('a', '2026-10-20T10:00:00Z'), ticket('b', null)], NOW);

      expect(upcoming.map((x) => x.sessionId)).toEqual(['a', 'b']);
      expect(past).toEqual([]);
   });

   test('a session that started more than eight hours ago is past, one that started a little ago is still upcoming', () => {
      const { upcoming, past } = splitTickets(
         [ticket('old', '2026-10-19T02:00:00Z'), ticket('running', '2026-10-19T10:00:00Z')],
         NOW,
      );

      expect(past.map((x) => x.sessionId)).toEqual(['old']);
      expect(upcoming.map((x) => x.sessionId)).toEqual(['running']);
   });

   test('a called off session is past, however soon it was to start', () => {
      const { upcoming, past } = splitTickets([ticket('x', '2026-10-25T10:00:00Z', 'cancelled')], NOW);

      expect(upcoming).toEqual([]);
      expect(past).toHaveLength(1);
   });

   test('the next session comes first, sessions without a time last', () => {
      const { upcoming } = splitTickets(
         [ticket('later', '2026-10-22T10:00:00Z'), ticket('none', null), ticket('soon', '2026-10-20T10:00:00Z')],
         NOW,
      );

      expect(upcoming.map((x) => x.sessionId)).toEqual(['soon', 'later', 'none']);
   });
});

describe('initialsOf', () => {
   test.each([
      ['Ada Lovelace', 'AL'],
      ['kofi', 'K'],
      ['  Ngozi  Eze  ', 'NE'],
      ['Mary Jane Watson', 'MW'],
      ['', '?'],
   ])('%s is %s', (name, expected) => {
      expect(initialsOf(name)).toBe(expected);
   });
});

describe('categoryStyle', () => {
   test('every category has its own colours, an unknown one looks like music', () => {
      const colours = ['teach', 'perform', 'expertise', 'community', 'music'].map((c) => categoryStyle(c).from);
      expect(new Set(colours).size).toBe(5);
      expect(categoryStyle('whatever')).toBe(categoryStyle('music'));
   });
});
