import { DateTime } from 'luxon';
import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({ fetchDiscover: vi.fn() }));

vi.mock('react-i18next', () => ({
   useTranslation: () => ({
      t: (key: string, values?: Record<string, unknown>) => (values ? `${key} ${JSON.stringify(values)}` : key),
   }),
}));
vi.mock('react-router-dom', () => ({
   Link: React.forwardRef<HTMLAnchorElement, { to: string; children: React.ReactNode }>(
      ({ to, children, component: _c, ...rest }: any, ref) => (
         <a ref={ref} href={to} className={rest.className}>
            {children}
         </a>
      ),
   ),
}));
vi.mock('src/services/api/sessions', () => api);

import DiscoverSessions, { isLiveNow } from './DiscoverSessions';

const session = (over: Record<string, unknown> = {}) => ({
   sessionId: 'abcd2345',
   title: 'Piano for beginners',
   category: 'music',
   priceMinor: 500000,
   currency: 'NGN',
   startsAt: null,
   creatorName: 'Ada',
   isHost: false,
   hasTicket: false,
   ...over,
});

let container: HTMLDivElement;
beforeEach(() => {
   container = document.createElement('div');
   document.body.appendChild(container);
});
afterEach(() => {
   container.remove();
   document.body.innerHTML = '';
   vi.clearAllMocks();
});

test('isLiveNow: started sessions are live, coming and undated ones are not', () => {
   const now = DateTime.fromISO('2026-05-01T12:00:00Z');
   expect(isLiveNow(session({ startsAt: '2026-05-01T11:00:00Z' }), now)).toBe(true);
   expect(isLiveNow(session({ startsAt: '2026-05-01T13:00:00Z' }), now)).toBe(false);
   expect(isLiveNow(session(), now)).toBe(false);
});

test('every session links to its page, live ones first', async () => {
   const past = DateTime.now().minus({ hours: 1 }).toISO();
   const future = DateTime.now().plus({ days: 1 }).toISO();
   api.fetchDiscover.mockResolvedValue([
      session({ sessionId: 'soon0001', title: 'Coming up', startsAt: future }),
      session({ sessionId: 'live0001', title: 'Happening', startsAt: past }),
   ]);

   await act(async () => createRoot(container).render(<DiscoverSessions />));

   const tiles = [...document.querySelectorAll('.discover-session')];
   expect(tiles.map((x) => x.getAttribute('href'))).toEqual(['/s/live0001', '/s/soon0001']);
   expect(document.querySelectorAll('.discover-live')).toHaveLength(1);
});

test('nothing on: says so', async () => {
   api.fetchDiscover.mockResolvedValue([]);
   await act(async () => createRoot(container).render(<DiscoverSessions />));
   expect(document.querySelector('#discover-empty')).not.toBeNull();
});

test('the call to the server fails: an error is shown, not an empty list', async () => {
   api.fetchDiscover.mockRejectedValue(new Error('down'));
   await act(async () => createRoot(container).render(<DiscoverSessions />));
   expect(document.querySelector('#discover-empty')).toBeNull();
   expect(document.body.textContent).toContain('sessions.discover_failed');
});
