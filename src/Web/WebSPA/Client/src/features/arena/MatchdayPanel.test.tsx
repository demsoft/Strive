import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({ fetchMySessions: vi.fn(), setScoreboard: vi.fn() }));
const push = vi.hoisted(() => vi.fn());

vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }) }));
vi.mock('react-router-dom', () => ({
   Link: ({ children, id }: { children: React.ReactNode; id?: string }) => <a id={id}>{children}</a>,
   useHistory: () => ({ push }),
}));
vi.mock('src/services/api/sessions', () => api);
vi.mock('src/features/sessions/components/OrganizerRequest', () => ({ default: () => <div id="organizer-request" /> }));

import MatchdayPanel, { nextMatchOf } from './MatchdayPanel';

const session = (id: string, over: Record<string, unknown> = {}) => ({
   sessionId: id,
   conferenceId: `room-${id}`,
   title: id,
   category: 'match',
   priceMinor: 300000,
   currency: 'NGN',
   status: 'published',
   startsAt: '2030-01-01T10:00:00Z',
   durationMinutes: 120,
   createdAt: '2026-11-01T10:00:00Z',
   refunded: 0,
   refundsPending: 0,
   match: { homeTeam: 'Lagos FC', awayTeam: 'Abuja United', competition: 'State Cup' },
   ...over,
});

test('the match in hand: not at full time, the earliest start; else the latest played', () => {
   const list = [
      session('later', { startsAt: '2030-02-01T10:00:00Z' }),
      session('soon', { startsAt: '2030-01-01T10:00:00Z' }),
      session('over', { startsAt: '2029-01-01T10:00:00Z', match: { homeTeam: 'A', awayTeam: 'B', period: 'ft' } }),
      session('called-off', { status: 'cancelled' }),
      session('music', { match: null }),
   ] as any[];

   expect(nextMatchOf(list)?.sessionId).toBe('soon');
   expect(nextMatchOf([list[2]])?.sessionId).toBe('over');
   expect(nextMatchOf([list[3], list[4]])).toBeNull();
});

const settle = () => act(async () => {});
let container: HTMLDivElement;
let root: ReturnType<typeof createRoot> | null = null;
beforeEach(() => {
   container = document.createElement('div');
   document.body.appendChild(container);
});
afterEach(async () => {
   await act(async () => root?.unmount());
   root = null;
   container.remove();
   document.body.innerHTML = '';
   vi.clearAllMocks();
});
const render = async (organizerStatus: string) => {
   root = createRoot(container);
   await act(async () =>
      root!.render(<MatchdayPanel profile={{ organizerStatus } as any} onProfileChanged={vi.fn()} />),
   );
   await settle();
};

test('an organizer who is not approved sees the request, and no matches are asked for', async () => {
   await render('requested');

   expect(container.querySelector('#organizer-request')).not.toBeNull();
   expect(container.querySelector('#matchday-panel')).toBeNull();
   expect(api.fetchMySessions).not.toHaveBeenCalled();
});

test('an approved organizer with no match is sent to set one up', async () => {
   api.fetchMySessions.mockResolvedValue([]);
   await render('approved');

   expect(container.querySelector('#matchday-none')).not.toBeNull();
   expect(container.querySelector('#organizer-approved')).not.toBeNull();
});

test('the match in hand has its scoreboard buttons and the way into the room', async () => {
   api.fetchMySessions.mockResolvedValue([session('s1')]);
   await render('approved');

   expect(container.querySelector('#scoreboard-controls')).not.toBeNull();
   await act(async () => (container.querySelector('#desk-open-room') as HTMLElement).click());
   expect(push).toHaveBeenCalledWith('/c/room-s1');
});
