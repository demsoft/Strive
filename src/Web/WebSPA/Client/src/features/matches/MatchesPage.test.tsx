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
   Link: ({ children, className }: { children: React.ReactNode; className?: string }) => (
      <a className={className}>{children}</a>
   ),
}));
vi.mock('src/features/creators/SiteHeader', () => ({ default: () => null }));
vi.mock('src/services/api/sessions', () => api);

import MatchesPage from './MatchesPage';

const match = (
   id: string,
   home: string,
   away: string,
   competition: string | null,
   over: Record<string, unknown> = {},
) => ({
   sessionId: id,
   title: `${home} vs ${away}`,
   category: 'match',
   priceMinor: 300000,
   currency: 'NGN',
   startsAt: null,
   creatorName: 'Lagos League',
   isHost: false,
   hasTicket: false,
   durationMinutes: 120,
   isLive: false,
   match: { homeTeam: home, awayTeam: away, competition },
   ...over,
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

const render = async () => {
   root = createRoot(container);
   await act(async () => root!.render(<MatchesPage />));
   await settle();
};

test('asks for matches only and lists them, live ones first', async () => {
   api.fetchDiscover.mockResolvedValue([
      match('a', 'Lagos FC', 'Abuja United', 'State Cup', { startsAt: '2030-01-01T10:00:00Z' }),
      match('b', 'Ibadan Lions', 'Ikorodu Stars', 'Schools League', { isLive: true }),
   ]);
   await render();

   expect(api.fetchDiscover).toHaveBeenCalledWith('match');
   const cards = [...document.querySelectorAll('.match-card')];
   expect(cards).toHaveLength(2);
   expect(cards[0].textContent).toContain('Ibadan Lions');
   expect(cards[0].querySelector('.match-live')).not.toBeNull();
});

test('a competition chip narrows the list, and the filters can be cleared', async () => {
   api.fetchDiscover.mockResolvedValue([
      match('a', 'Lagos FC', 'Abuja United', 'State Cup'),
      match('b', 'Ibadan Lions', 'Ikorodu Stars', 'Schools League'),
   ]);
   await render();

   const chip = [...document.querySelectorAll('.match-competition-chip')].find((x) =>
      x.textContent?.includes('Schools League'),
   ) as HTMLElement;
   await act(async () => chip.click());

   const cards = document.querySelectorAll('.match-card');
   expect(cards).toHaveLength(1);
   expect(cards[0].textContent).toContain('Ibadan Lions');

   await act(async () => (document.querySelector('#matches-when-live') as HTMLElement).click());
   expect(document.querySelector('#matches-no-match')).not.toBeNull();
});

test('nothing on: says so; a failure is shown', async () => {
   api.fetchDiscover.mockResolvedValue([]);
   await render();
   expect(document.querySelector('#matches-none')).not.toBeNull();

   await act(async () => root!.unmount());
   root = null;
   api.fetchDiscover.mockRejectedValue(new Error('x'));
   await render();
   expect(document.querySelector('#matches-failed')).not.toBeNull();
});
