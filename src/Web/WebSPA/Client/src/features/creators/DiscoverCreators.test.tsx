import React, { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({ fetchCreatorCards: vi.fn(), followCreator: vi.fn(), unfollowCreator: vi.fn() }));
const state = vi.hoisted(() => ({
   user: undefined as undefined | { access_token: string },
   signinRedirect: vi.fn(),
}));

vi.mock('react-i18next', () => ({
   useTranslation: () => ({
      t: (key: string, values?: Record<string, unknown>) => (values ? `${key} ${JSON.stringify(values)}` : key),
   }),
}));
vi.mock('react-oidc-context', () => ({
   useAuth: () => ({ user: state.user, isLoading: false, signinRedirect: state.signinRedirect }),
}));
vi.mock('react-router-dom', () => ({
   Link: React.forwardRef<
      HTMLAnchorElement,
      { to: string; children: React.ReactNode; className?: string; id?: string }
   >(({ to, children, className, id }, ref) => (
      <a ref={ref} href={to} className={className} id={id}>
         {children}
      </a>
   )),
   useLocation: () => ({ pathname: '/', search: '' }),
}));
vi.mock('src/services/api/follows', () => api);

import DiscoverCreators from './DiscoverCreators';

const card = (id: string, over: Record<string, unknown> = {}) => ({
   creator: { creatorId: id, displayName: 'Creator ' + id },
   followers: 40,
   following: false,
   isYou: false,
   videos: 1,
   sessions: 0,
   ...over,
});

let container: HTMLDivElement;
let root: Root;
const settle = () => act(async () => undefined);
const render = async () => {
   await act(async () => root.render(<DiscoverCreators />));
   await settle();
};

beforeEach(() => {
   container = document.createElement('div');
   document.body.appendChild(container);
   root = createRoot(container);
   state.user = undefined;
   api.fetchCreatorCards.mockResolvedValue({ items: [card('a'), card('b')], total: 2 });
   api.followCreator.mockResolvedValue(undefined);
   api.unfollowCreator.mockResolvedValue(undefined);
});
afterEach(() => {
   act(() => root.unmount());
   container.remove();
   document.body.innerHTML = '';
   vi.clearAllMocks();
});

test('shows the popular creators and the way to all of them', async () => {
   await render();

   expect(api.fetchCreatorCards).toHaveBeenCalledWith({ sort: 'popular', limit: 12 });
   expect([...document.querySelectorAll('.creator-chip')]).toHaveLength(2);
   expect(document.querySelector('#discover-creators-all')?.getAttribute('href')).toBe('/creators');
});

test('nothing is shown while there are no creators, or when they cannot be loaded', async () => {
   api.fetchCreatorCards.mockResolvedValue({ items: [], total: 0 });
   await render();
   expect(document.querySelector('#discover-creators')).toBeNull();

   act(() => root.unmount());
   root = createRoot(container);
   api.fetchCreatorCards.mockRejectedValue(new Error('down'));
   await render();
   expect(document.querySelector('#discover-creators')).toBeNull();
});

test('Follow works from the home page; the own card has none', async () => {
   state.user = { access_token: 'tok' };
   api.fetchCreatorCards.mockResolvedValue({ items: [card('a'), card('me', { isYou: true })], total: 2 });
   await render();
   expect(document.querySelectorAll('.creator-chip-follow')).toHaveLength(1);

   await act(async () => document.querySelector<HTMLElement>('.creator-chip-follow')?.click());

   expect(api.followCreator).toHaveBeenCalledWith('a');
   expect(document.querySelector('.creator-chip-follow')?.getAttribute('aria-pressed')).toBe('true');
});
