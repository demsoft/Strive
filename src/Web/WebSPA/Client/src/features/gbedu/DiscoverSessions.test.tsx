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

import DiscoverSessions, { filterSessions } from './DiscoverSessions';

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
   durationMinutes: 60,
   isLive: false,
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

test('every session links to its page, live ones first', async () => {
   const past = DateTime.now().minus({ hours: 1 }).toISO();
   const future = DateTime.now().plus({ days: 1 }).toISO();
   api.fetchDiscover.mockResolvedValue([
      session({ sessionId: 'soon0001', title: 'Coming up', startsAt: future }),
      // the start time has passed but the host has not opened the room: not live
      session({ sessionId: 'late0001', title: 'Not open yet', startsAt: past }),
      session({ sessionId: 'live0001', title: 'Happening', startsAt: past, isLive: true }),
   ]);

   await act(async () => createRoot(container).render(<DiscoverSessions />));

   const tiles = [...document.querySelectorAll('.discover-session')];
   expect(tiles.map((x) => x.getAttribute('href'))).toEqual(['/s/live0001', '/s/late0001', '/s/soon0001']);
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

const setValue = async (selector: string, value: string) => {
   const input = document.querySelector(selector) as HTMLInputElement;
   const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!;
   await act(async () => {
      setter.call(input, value);
      input.dispatchEvent(new Event('input', { bubbles: true }));
   });
};

const catalog = [
   session({ sessionId: 'a', title: 'Piano for beginners', category: 'music', creatorName: 'Ada Lovelace' }),
   session({
      sessionId: 'b',
      title: 'Café économie',
      category: 'expertise',
      creatorName: 'Kofi',
      description: 'Money talk',
   }),
   session({ sessionId: 'c', title: 'Drum circle', category: 'music', creatorName: 'Ngozi' }),
];

test('filterSessions: words are matched in title, text and host, without capitals or accents', () => {
   const ids = (q: string, c: string | null = null) => filterSessions(catalog as any, q, c).map((x) => x.sessionId);

   expect(ids('')).toEqual(['a', 'b', 'c']);
   expect(ids('PIANO')).toEqual(['a']);
   expect(ids('lovelace')).toEqual(['a']);
   expect(ids('cafe economie')).toEqual(['b']);
   expect(ids('money')).toEqual(['b']);
   expect(ids('piano drum')).toEqual([]);
});

test('filterSessions: the category narrows it down, together with the words', () => {
   expect(filterSessions(catalog as any, '', 'music').map((x) => x.sessionId)).toEqual(['a', 'c']);
   expect(filterSessions(catalog as any, 'drum', 'music').map((x) => x.sessionId)).toEqual(['c']);
   expect(filterSessions(catalog as any, 'drum', 'teach')).toEqual([]);
});

test('typing and choosing a category change what is shown, and nothing matching says so', async () => {
   api.fetchDiscover.mockResolvedValue(catalog);
   await act(async () => createRoot(container).render(<DiscoverSessions />));
   await act(async () => {});
   const shown = () => [...document.querySelectorAll('.discover-session')].map((x) => x.getAttribute('href'));

   expect(shown()).toHaveLength(3);
   await act(async () => (document.querySelector('#discover-cat-music') as HTMLElement).click());
   expect(shown()).toEqual(['/s/a', '/s/c']);
   await setValue('#discover-search', 'drum');
   expect(shown()).toEqual(['/s/c']);
   await setValue('#discover-search', 'nothing like this');
   expect(shown()).toEqual([]);
   expect(document.querySelector('#discover-no-match')).not.toBeNull();

   // the same category again clears it, "all" too
   await setValue('#discover-search', '');
   await act(async () => (document.querySelector('#discover-cat-all') as HTMLElement).click());
   expect(shown()).toHaveLength(3);
   expect(document.querySelector('#discover-no-match')).toBeNull();
});

test('no filters while there is nothing on', async () => {
   api.fetchDiscover.mockResolvedValue([]);
   await act(async () => createRoot(container).render(<DiscoverSessions />));
   await act(async () => {});

   expect(document.querySelector('#discover-filters')).toBeNull();
});
