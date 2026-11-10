import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({ fetchDiscover: vi.fn() }));
const auth = vi.hoisted(() => ({ isAuthenticated: false, signinRedirect: vi.fn() }));

vi.mock('react-i18next', () => ({
   useTranslation: () => ({
      t: (key: string, values?: Record<string, unknown>) => (values ? `${key} ${JSON.stringify(values)}` : key),
   }),
}));
vi.mock('react-oidc-context', () => ({ useAuth: () => auth }));
vi.mock('react-router-dom', () => ({
   Link: ({ children, className, id }: { children: React.ReactNode; className?: string; id?: string }) => (
      <a className={className} id={id}>
         {children}
      </a>
   ),
}));
vi.mock('src/features/creators/SiteHeader', () => ({ default: () => null }));
vi.mock('./ClubsRow', () => ({ default: () => <div id="clubs-row" /> }));
vi.mock('src/services/api/sessions', () => api);

import ArenaHome from './ArenaHome';

const match = (id: string, over: Record<string, unknown> = {}) => ({
   sessionId: id,
   title: id,
   category: 'match',
   priceMinor: 300000,
   currency: 'NGN',
   startsAt: '2030-01-01T10:00:00Z',
   creatorName: 'Lagos League',
   isHost: false,
   hasTicket: false,
   durationMinutes: 120,
   isLive: false,
   match: { homeTeam: 'Lagos FC', awayTeam: 'Abuja United', competition: 'State Cup' },
   ...over,
});

const settle = () => act(async () => {});
let container: HTMLDivElement;
let root: ReturnType<typeof createRoot> | null = null;
beforeEach(() => {
   container = document.createElement('div');
   document.body.appendChild(container);
   auth.isAuthenticated = false;
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
   await act(async () => root!.render(<ArenaHome />));
   await settle();
};

test('asks for matches only; the live match is the featured one with its score', async () => {
   api.fetchDiscover.mockResolvedValue([
      match('later'),
      match('live', {
         isLive: true,
         match: {
            homeTeam: 'Ibadan',
            awayTeam: 'Ikorodu',
            competition: 'Schools League',
            homeScore: 1,
            awayScore: 0,
            period: '1h',
         },
      }),
   ]);
   await render();

   expect(api.fetchDiscover).toHaveBeenCalledWith('match');
   const featured = document.querySelector('#arena-featured')!;
   expect(featured.textContent).toContain('Ibadan');
   expect(featured.querySelector('.scoreboard-score')?.textContent).toBe('1 : 0');
   expect(container.textContent).toContain('State Cup');
});

test('no match: says so, and the clubs are still shown', async () => {
   api.fetchDiscover.mockResolvedValue([]);
   await render();

   expect(document.querySelector('#arena-empty')).not.toBeNull();
   expect(document.querySelector('#arena-featured')).toBeNull();
   expect(document.querySelector('#clubs-row')).not.toBeNull();
});

test('a visitor is asked to become an organizer, a signed in person is not', async () => {
   api.fetchDiscover.mockResolvedValue([]);
   await render();
   expect(document.querySelector('#arena-organizers-cta')).not.toBeNull();

   await act(async () => (document.querySelector('#arena-become-organizer') as HTMLElement).click());
   expect(auth.signinRedirect).toHaveBeenCalledWith({ state: { url: '/creator' }, prompt: 'create' });

   await act(async () => root!.unmount());
   root = null;
   auth.isAuthenticated = true;
   await render();
   expect(document.querySelector('#arena-organizers-cta')).toBeNull();
});

test('a failure is shown', async () => {
   api.fetchDiscover.mockRejectedValue(new Error('x'));
   await render();

   expect(document.querySelector('#arena-failed')).not.toBeNull();
});
