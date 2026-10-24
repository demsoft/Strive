import React, { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({
   fetchCreatorCards: vi.fn(),
   fetchMyFollowers: vi.fn(),
   followCreator: vi.fn(),
   unfollowCreator: vi.fn(),
}));
const state = vi.hoisted(() => ({ search: '', replace: vi.fn() }));

vi.mock('react-i18next', () => ({
   useTranslation: () => ({
      t: (key: string, values?: Record<string, unknown>) => (values ? `${key} ${JSON.stringify(values)}` : key),
   }),
}));
vi.mock('react-oidc-context', () => ({ useAuth: () => ({ user: { access_token: 'tok' }, isLoading: false }) }));
vi.mock('react-router-dom', () => ({
   Link: React.forwardRef<
      HTMLAnchorElement,
      { to: string; children: React.ReactNode; id?: string; className?: string }
   >(({ to, children, id, className }, ref) => (
      <a ref={ref} href={to} id={id} className={className}>
         {children}
      </a>
   )),
   useLocation: () => ({ pathname: '/connections', search: state.search }),
   useHistory: () => ({ replace: state.replace }),
}));
vi.mock('src/features/creators/SiteHeader', () => ({ default: () => null }));
vi.mock('src/features/creator/errors', () => ({
   requestErrorMessage: (e: unknown) => String((e as Error).message ?? e),
}));
vi.mock('src/services/api/follows', () => api);

import ConnectionsPage from './ConnectionsPage';

const card = (id: string, over: Record<string, unknown> = {}) => ({
   creator: { creatorId: id, displayName: 'Creator ' + id },
   followers: 10,
   following: true,
   isYou: false,
   videos: 1,
   sessions: 0,
   ...over,
});
const follower = (name: string) => ({ name, since: '2026-10-20T10:00:00Z' });

let container: HTMLDivElement;
let root: Root;
const settle = () => act(async () => undefined);
const render = async () => {
   await act(async () => root.render(<ConnectionsPage />));
   await settle();
};
const click = (selector: string) => act(async () => document.querySelector<HTMLElement>(selector)?.click());

beforeEach(() => {
   container = document.createElement('div');
   document.body.appendChild(container);
   root = createRoot(container);
   state.search = '';
   api.fetchCreatorCards.mockResolvedValue({ items: [card('a'), card('b')], total: 2 });
   api.fetchMyFollowers.mockResolvedValue({ items: [follower('Tola'), follower('Bayo')], total: 2 });
   api.followCreator.mockResolvedValue(undefined);
   api.unfollowCreator.mockResolvedValue(undefined);
});
afterEach(() => {
   act(() => root.unmount());
   container.remove();
   document.body.innerHTML = '';
   vi.clearAllMocks();
});

test('shows the creators the person follows, and how many', async () => {
   await render();

   expect(api.fetchCreatorCards).toHaveBeenCalledWith(expect.objectContaining({ following: true }));
   expect(document.querySelectorAll('.creator-card')).toHaveLength(2);
   expect(document.querySelector('#connections-tab-following')?.textContent).toContain('· 2');
});

test('nobody followed yet: it says so and leads to the creators', async () => {
   api.fetchCreatorCards.mockResolvedValue({ items: [], total: 0 });
   await render();

   expect(document.querySelector('#connections-following-none')).not.toBeNull();
   expect(document.querySelector('#connections-find')?.getAttribute('href')).toBe('/creators');
});

test('stopping to follow from here, and following again, work at once; a refusal goes back with the reason', async () => {
   await render();

   await click('.creator-card-follow');
   expect(api.unfollowCreator).toHaveBeenCalledWith('a');
   expect(document.querySelector('.creator-card-follow')?.getAttribute('aria-pressed')).toBe('false');

   api.followCreator.mockRejectedValue(new Error('Not now.'));
   await click('.creator-card-follow');
   await settle();
   expect(document.querySelector('.creator-card-follow')?.getAttribute('aria-pressed')).toBe('false');
   expect(document.body.textContent).toContain('Not now.');
});

test('a creator has a followers tab with the people and when they followed', async () => {
   state.search = '?tab=followers';
   await render();

   expect(document.querySelector('#connections-tab-followers')?.textContent).toContain('· 2');
   expect([...document.querySelectorAll('.follower-name')].map((x) => x.textContent)).toEqual(['Tola', 'Bayo']);
   expect(document.querySelector('.follower-since')?.textContent).toContain('social.connections_followers_since');
});

test('a person who is no creator has no followers tab, even when the link asks for it', async () => {
   state.search = '?tab=followers';
   api.fetchMyFollowers.mockResolvedValue(null);
   await render();

   expect(document.querySelector('#connections-tab-followers')).toBeNull();
   expect(document.querySelector('#connections-following, #connections-following-none, .creator-card')).not.toBeNull();
});

test('a creator with nobody following yet is told how to get followers', async () => {
   state.search = '?tab=followers';
   api.fetchMyFollowers.mockResolvedValue({ items: [], total: 0 });
   await render();

   expect(document.querySelector('#connections-followers-none')).not.toBeNull();
   expect(document.querySelector('#connections-your-page')?.getAttribute('href')).toBe('/creator');
});

test('more followers are loaded on request', async () => {
   state.search = '?tab=followers';
   api.fetchMyFollowers
      .mockResolvedValueOnce({ items: [follower('Tola'), follower('Bayo')], total: 3 })
      .mockResolvedValueOnce({ items: [follower('Kofi')], total: 3 });
   await render();

   await click('#connections-more');

   expect(api.fetchMyFollowers).toHaveBeenLastCalledWith(2, 50);
   expect([...document.querySelectorAll('.follower-name')].map((x) => x.textContent)).toEqual(['Tola', 'Bayo', 'Kofi']);
   expect(document.querySelector('#connections-more')).toBeNull();
});

test('the tabs switch by the address', async () => {
   await render();

   await click('#connections-tab-followers');

   expect(state.replace).toHaveBeenCalledWith('/connections?tab=followers');
});
