import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({
   fetchCreatorPage: vi.fn(),
   fetchPayment: vi.fn(),
   startPassCheckout: vi.fn(),
   creatorPageLink: (id: string) => `https://gbedu.test/u/${id}`,
}));

vi.mock('react-i18next', () => ({
   useTranslation: () => ({
      t: (key: string, values?: Record<string, unknown>) => (values ? `${key} ${JSON.stringify(values)}` : key),
   }),
}));
const auth = vi.hoisted(() => ({
   user: undefined as undefined | { access_token: string },
   signinRedirect: vi.fn(),
   follow: vi.fn(),
   unfollow: vi.fn(),
}));
vi.mock('react-oidc-context', () => ({
   useAuth: () => ({ user: auth.user, isLoading: false, signinRedirect: auth.signinRedirect }),
}));
vi.mock('src/features/social/NoticeSettings', () => ({
   default: (props: { state: string; creatorName: string }) => (
      <div id="notice-stub" data-state={props.state} data-name={props.creatorName} />
   ),
}));
vi.mock('src/services/api/follows', () => ({
   followCreator: (...a: unknown[]) => auth.follow(...a),
   unfollowCreator: (...a: unknown[]) => auth.unfollow(...a),
}));
vi.mock('react-router-dom', () => ({
   Link: React.forwardRef<HTMLAnchorElement, { to: string; children: React.ReactNode }>(
      ({ to, children, ...rest }: any, ref) => (
         <a ref={ref} href={to} className={rest.className}>
            {children}
         </a>
      ),
   ),
   useParams: () => ({ creatorId: 'c1' }),
   useLocation: () => ({ pathname: '/u/c1', search: '' }),
}));
vi.mock('src/components/BrandLogo', () => ({ default: () => null }));
vi.mock('src/services/api/sessions', () => api);

vi.mock('../errors', () => ({ requestErrorMessage: (e: unknown) => String((e as Error).message ?? e) }));

import CreatorPublicPage from './CreatorPublicPage';

const settle = () => act(async () => {});
const session = (id: string) => ({
   sessionId: id,
   title: 'Piano ' + id,
   category: 'music',
   priceMinor: 500000,
   currency: 'NGN',
   startsAt: null,
   creatorName: 'Ada',
   isHost: false,
   hasTicket: false,
});

let container: HTMLDivElement;
beforeEach(() => {
   auth.user = undefined;
   container = document.createElement('div');
   document.body.appendChild(container);
});
afterEach(() => {
   container.remove();
   document.body.innerHTML = '';
   vi.clearAllMocks();
});

test('shows the creator and links every session to its page', async () => {
   api.fetchCreatorPage.mockResolvedValue({
      creator: { creatorId: 'c1', displayName: 'Ada Lovelace', bio: 'Piano teacher' },
      sessions: [session('aaaa1111'), session('bbbb2222')],
      hasPass: false,
   });
   await act(async () => createRoot(container).render(<CreatorPublicPage />));
   await settle();

   expect(document.querySelector('#creator-name')?.textContent).toBe('Ada Lovelace');
   expect(document.querySelector('#creator-bio')?.textContent).toBe('Piano teacher');
   expect([...document.querySelectorAll('.discover-session')].map((x) => x.getAttribute('href'))).toEqual([
      '/s/aaaa1111',
      '/s/bbbb2222',
   ]);
   expect(api.fetchCreatorPage).toHaveBeenCalledWith('c1', undefined);
});

test('a creator with nothing on says so', async () => {
   api.fetchCreatorPage.mockResolvedValue({
      creator: { creatorId: 'c1', displayName: 'Ada' },
      sessions: [],
      hasPass: false,
   });
   await act(async () => createRoot(container).render(<CreatorPublicPage />));
   await settle();

   expect(document.querySelector('#creator-nothing-on')).not.toBeNull();
});

test('an unknown creator shows the not found message', async () => {
   api.fetchCreatorPage.mockRejectedValue(new Error('404'));
   await act(async () => createRoot(container).render(<CreatorPublicPage />));
   await settle();

   expect(document.querySelector('#creator-public-page')?.textContent).toContain('creator.page_not_found');
});

test('the share button copies the link of the page', async () => {
   api.fetchCreatorPage.mockResolvedValue({
      creator: { creatorId: 'c1', displayName: 'Ada' },
      sessions: [],
      hasPass: false,
   });
   const writeText = vi.fn().mockResolvedValue(undefined);
   Object.assign(navigator, { clipboard: { writeText } });
   await act(async () => createRoot(container).render(<CreatorPublicPage />));
   await settle();

   await act(async () => (document.querySelector('#creator-share') as HTMLElement).click());

   expect(writeText).toHaveBeenCalledWith('https://gbedu.test/u/c1');
});

test('the videos of the creator are shown above the sessions', async () => {
   api.fetchCreatorPage.mockResolvedValue({
      creator: { creatorId: 'c1', displayName: 'Ada' },
      sessions: [],
      hasPass: false,
      videos: [
         { videoId: 'v1', title: 'First reel', status: 'ready', priceMinor: 0, createdAt: '', isOwner: false },
         { videoId: 'v2', title: 'Second reel', status: 'ready', priceMinor: 0, createdAt: '', isOwner: false },
      ],
   });
   await act(async () => createRoot(container).render(<CreatorPublicPage />));
   await settle();

   expect([...document.querySelectorAll('#creator-videos .video-tile')].map((x) => x.getAttribute('href'))).toEqual([
      '/v/v1',
      '/v/v2',
   ]);
});

test('a creator without videos has no video section', async () => {
   api.fetchCreatorPage.mockResolvedValue({
      creator: { creatorId: 'c1', displayName: 'Ada' },
      sessions: [],
      hasPass: false,
      videos: [],
   });
   await act(async () => createRoot(container).render(<CreatorPublicPage />));
   await settle();

   expect(document.querySelector('#creator-videos')).toBeNull();
});

const followButton = () => document.querySelector<HTMLElement>('.creator-follow');
const followersText = () => document.querySelector('#creator-followers')?.textContent;
const pageOf = (over: Record<string, unknown> = {}) => ({
   creator: { creatorId: 'c1', displayName: 'Ada' },
   sessions: [],
   hasPass: false,
   followers: 4,
   following: false,
   ...over,
});

test('the page shows how many follow, and a visitor who follows is asked to sign in', async () => {
   api.fetchCreatorPage.mockResolvedValue(pageOf());
   await act(async () => createRoot(container).render(<CreatorPublicPage />));
   await settle();

   expect(followersText()).toContain('"count":4');
   await act(async () => followButton()?.click());
   expect(auth.signinRedirect).toHaveBeenCalledWith({ state: { url: '/u/c1' } });
   expect(auth.follow).not.toHaveBeenCalled();
});

test('following adds one at once; stopping takes it away; a refusal goes back', async () => {
   auth.user = { access_token: 'tok' };
   api.fetchCreatorPage.mockResolvedValue(pageOf());
   await act(async () => createRoot(container).render(<CreatorPublicPage />));
   await settle();

   await act(async () => followButton()?.click());
   expect(auth.follow).toHaveBeenCalledWith('c1');
   expect(followersText()).toContain('"count":5');
   expect(followButton()?.getAttribute('aria-pressed')).toBe('true');

   await act(async () => followButton()?.click());
   expect(auth.unfollow).toHaveBeenCalledWith('c1');
   expect(followersText()).toContain('"count":4');

   auth.follow.mockRejectedValueOnce(new Error('down'));
   await act(async () => followButton()?.click());
   await settle();
   expect(followersText()).toContain('"count":4');
   expect(followButton()?.getAttribute('aria-pressed')).toBe('false');
});

test('a person who follows already sees it, and the creator has no follow button on their own page', async () => {
   auth.user = { access_token: 'tok' };
   api.fetchCreatorPage.mockResolvedValue(pageOf({ following: true }));
   const root = createRoot(container);
   await act(async () => root.render(<CreatorPublicPage />));
   await settle();
   expect(followButton()?.getAttribute('aria-pressed')).toBe('true');
   act(() => root.unmount());

   api.fetchCreatorPage.mockResolvedValue(pageOf({ isYou: true }));
   const own = createRoot(container);
   await act(async () => own.render(<CreatorPublicPage />));
   await settle();
   expect(followButton()).toBeNull();
   act(() => own.unmount());
});

test('a follower sees the settings for mail when the server can send mail, with what they chose', async () => {
   auth.user = { access_token: 'tok' };
   api.fetchCreatorPage.mockResolvedValue(pageOf({ following: true, noticesAvailable: true, notice: 'pending' }));
   await act(async () => createRoot(container).render(<CreatorPublicPage />));
   await settle();

   const stub = document.querySelector('#notice-stub');
   expect(stub?.getAttribute('data-state')).toBe('pending');
   expect(stub?.getAttribute('data-name')).toBe('Ada');
});

test('no settings for mail for somebody who does not follow, on the own page, or when mail is not set up', async () => {
   auth.user = { access_token: 'tok' };
   for (const over of [
      { following: false, noticesAvailable: true },
      { following: true, noticesAvailable: true, isYou: true },
      { following: true, noticesAvailable: false },
   ]) {
      container.innerHTML = '';
      api.fetchCreatorPage.mockResolvedValue(pageOf(over));
      const root = createRoot(container);
      await act(async () => root.render(<CreatorPublicPage />));
      await settle();
      expect(document.querySelector('#notice-stub')).toBeNull();
      act(() => root.unmount());
   }
});

test('stopping to follow ends the mail on the page as well', async () => {
   auth.user = { access_token: 'tok' };
   api.fetchCreatorPage.mockResolvedValue(pageOf({ following: true, noticesAvailable: true, notice: 'on' }));
   await act(async () => createRoot(container).render(<CreatorPublicPage />));
   await settle();
   expect(document.querySelector('#notice-stub')?.getAttribute('data-state')).toBe('on');

   await act(async () => followButton()?.click());

   // not following any more: the settings are gone, and when following again the mail is off
   expect(document.querySelector('#notice-stub')).toBeNull();
   await act(async () => followButton()?.click());
   expect(document.querySelector('#notice-stub')?.getAttribute('data-state')).toBe('off');
});

test('when following does not work the person is told why, and the button goes back', async () => {
   auth.user = { access_token: 'tok' };
   auth.follow.mockRejectedValue(new Error('You cannot follow yourself.'));
   api.fetchCreatorPage.mockResolvedValue(pageOf());
   await act(async () => createRoot(container).render(<CreatorPublicPage />));
   await settle();

   await act(async () => followButton()?.click());
   await settle();

   expect(document.querySelector('#follow-error')?.textContent).toContain('You cannot follow yourself.');
   expect(followButton()?.getAttribute('aria-pressed')).toBe('false');
});
