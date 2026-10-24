import React, { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({ fetchCreatorCards: vi.fn(), followCreator: vi.fn(), unfollowCreator: vi.fn() }));
const state = vi.hoisted(() => ({
   user: undefined as undefined | { access_token: string },
   isLoading: false,
   signinRedirect: vi.fn(),
}));

vi.mock('react-i18next', () => ({
   useTranslation: () => ({
      t: (key: string, values?: Record<string, unknown>) => (values ? `${key} ${JSON.stringify(values)}` : key),
   }),
}));
vi.mock('react-oidc-context', () => ({
   useAuth: () => ({ user: state.user, isLoading: state.isLoading, signinRedirect: state.signinRedirect }),
}));
vi.mock('react-router-dom', () => ({
   Link: React.forwardRef<HTMLAnchorElement, { to: string; children: React.ReactNode; className?: string }>(
      ({ to, children, className }, ref) => (
         <a ref={ref} href={to} className={className}>
            {children}
         </a>
      ),
   ),
   useLocation: () => ({ pathname: '/creators', search: '' }),
}));
vi.mock('./SiteHeader', () => ({ default: () => null }));
vi.mock('src/features/creator/errors', () => ({
   requestErrorMessage: (e: unknown) => String((e as Error).message ?? e),
}));
vi.mock('src/services/api/follows', () => api);

import CreatorsPage from './CreatorsPage';

const card = (id: string, over: Record<string, unknown> = {}) => ({
   creator: { creatorId: id, displayName: 'Creator ' + id, bio: 'Teaches ' + id, slug: id === 'a' ? 'ada' : undefined },
   followers: 1200,
   following: false,
   isYou: false,
   videos: 3,
   sessions: 1,
   coverUrl: 'https://r2.test/cover.jpg',
   ...over,
});

let container: HTMLDivElement;
let root: Root;
const settle = () => act(async () => undefined);
const wait = (ms: number) => act(async () => new Promise((resolve) => setTimeout(resolve, ms)));
const render = async () => {
   await act(async () => root.render(<CreatorsPage />));
   await settle();
};
const cards = () => [...document.querySelectorAll('.creator-card')];
const click = (selector: string) => act(async () => document.querySelector<HTMLElement>(selector)?.click());
const lastQuery = () => api.fetchCreatorCards.mock.calls[api.fetchCreatorCards.mock.calls.length - 1][0];

beforeEach(() => {
   container = document.createElement('div');
   document.body.appendChild(container);
   root = createRoot(container);
   state.user = undefined;
   state.isLoading = false;
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

test('shows the popular creators first: name, followers in short, what they have, and a link to their page', async () => {
   await render();

   expect(lastQuery()).toMatchObject({ sort: 'popular', following: false, skip: 0 });
   expect(cards()).toHaveLength(2);
   expect(document.querySelector('.creator-card-name')?.textContent).toBe('Creator a');
   expect(document.querySelector('.creator-card-followers')?.textContent).toContain('1.2K');
   expect(document.querySelector('.creator-card-videos')?.textContent).toContain('"count":3');
   expect(document.querySelector('.creator-card-sessions')?.textContent).toContain('"count":1');
   // the short name when there is one
   expect(document.querySelector('.creator-card-link')?.getAttribute('href')).toBe('/u/ada');
});

test('nothing is asked for until it is known whether the person is signed in', async () => {
   state.isLoading = true;
   await render();

   expect(api.fetchCreatorCards).not.toHaveBeenCalled();
});

test('the search waits for the person to stop typing', async () => {
   await render();
   const box = document.querySelector<HTMLInputElement>('#creators-search')!;
   const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!;

   await act(async () => {
      setter.call(box, '  piano ');
      box.dispatchEvent(new Event('input', { bubbles: true }));
   });
   expect(lastQuery().q).toBe('');
   await wait(400);

   expect(lastQuery()).toMatchObject({ q: 'piano', skip: 0 });
});

test('New asks for the newest first', async () => {
   await render();

   await click('#creators-view-new');

   expect(lastQuery()).toMatchObject({ sort: 'new', following: false });
});

test('Following needs a sign in; signed in it asks for the followed creators only', async () => {
   await render();
   await click('#creators-view-following');
   expect(state.signinRedirect).toHaveBeenCalledWith({ state: { url: '/creators' } });
   expect(lastQuery().following).toBe(false);

   act(() => root.unmount());
   root = createRoot(container);
   state.user = { access_token: 'tok' };
   await render();
   await click('#creators-view-following');

   expect(lastQuery().following).toBe(true);
});

test('Follow shows the new state and the new number at once, and asks the server', async () => {
   state.user = { access_token: 'tok' };
   await render();

   await click('.creator-card-follow');

   expect(api.followCreator).toHaveBeenCalledWith('a');
   expect(document.querySelector('.creator-card-follow')?.getAttribute('aria-pressed')).toBe('true');
   expect(document.querySelector('.creator-card-followers')?.textContent).toContain('1.2K'); // 1201 is still 1.2K

   await click('.creator-card-follow');
   expect(api.unfollowCreator).toHaveBeenCalledWith('a');
   expect(document.querySelector('.creator-card-follow')?.getAttribute('aria-pressed')).toBe('false');
});

test('when the server refuses, the button goes back', async () => {
   state.user = { access_token: 'tok' };
   api.followCreator.mockRejectedValue(new Error('down'));
   await render();

   await click('.creator-card-follow');
   await settle();

   expect(document.querySelector('.creator-card-follow')?.getAttribute('aria-pressed')).toBe('false');
});

test('when it does not work the person is told why, not left guessing', async () => {
   state.user = { access_token: 'tok' };
   api.followCreator.mockRejectedValue(new Error('You cannot follow yourself.'));
   await render();

   await click('.creator-card-follow');
   await settle();

   expect(document.body.textContent).toContain('You cannot follow yourself.');
});

test('signed out, Follow asks for a sign in and sends nothing', async () => {
   await render();

   await click('.creator-card-follow');

   expect(state.signinRedirect).toHaveBeenCalledWith({ state: { url: '/creators' } });
   expect(api.followCreator).not.toHaveBeenCalled();
});

test('the creator themself has no Follow button on their card', async () => {
   api.fetchCreatorCards.mockResolvedValue({ items: [card('a', { isYou: true })], total: 1 });
   await render();

   expect(document.querySelector('.creator-card-follow')).toBeNull();
   expect(document.querySelector('.creator-card-you')).not.toBeNull();
});

test('more creators are loaded on request, without repeating any', async () => {
   api.fetchCreatorCards
      .mockResolvedValueOnce({ items: [card('a'), card('b')], total: 3 })
      .mockResolvedValueOnce({ items: [card('b'), card('c')], total: 3 });
   await render();

   await click('#creators-more');

   expect(lastQuery().skip).toBe(2);
   expect(cards()).toHaveLength(3);
   expect(document.querySelector('#creators-more')).toBeNull();
});

test('no match, nobody followed yet, and a failure each say so', async () => {
   api.fetchCreatorCards.mockResolvedValue({ items: [], total: 0 });
   await render();
   expect(document.querySelector('#creators-none')?.textContent).toBe('creators.none');

   act(() => root.unmount());
   root = createRoot(container);
   state.user = { access_token: 'tok' };
   await render();
   await click('#creators-view-following');
   expect(document.querySelector('#creators-none')?.textContent).toBe('creators.none_following');

   act(() => root.unmount());
   root = createRoot(container);
   api.fetchCreatorCards.mockRejectedValue(new Error('down'));
   await render();
   expect(document.querySelector('#creators-failed')).not.toBeNull();
});
