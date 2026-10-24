import React, { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({
   fetchFeed: vi.fn(),
   fetchVideo: vi.fn(),
   likeVideo: vi.fn(),
   unlikeVideo: vi.fn(),
   videoLink: (id: string) => `https://gbedu.test/v/${id}`,
}));
const state = vi.hoisted(() => ({
   user: undefined as undefined | { access_token: string },
   isLoading: false,
   startId: undefined as undefined | string,
   search: '',
   following: new Set<string>(),
   toggle: vi.fn(),
   replace: vi.fn(),
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
   Link: React.forwardRef<
      HTMLAnchorElement,
      { to: string; children: React.ReactNode; className?: string; id?: string }
   >(({ to, children, className, id }, ref) => (
      <a ref={ref} href={to} className={className} id={id}>
         {children}
      </a>
   )),
   useParams: () => ({ videoId: state.startId }),
   useHistory: () => ({ replace: state.replace }),
   useLocation: () => ({ pathname: '/reels', search: state.search }),
}));
vi.mock('./ReportVideoDialog', () => ({
   default: ({ open, videoId }: { open: boolean; videoId: string }) =>
      open ? <div id="report-dialog" data-video={videoId} /> : null,
}));
vi.mock('src/features/social/useFollowing', () => ({
   default: () => ({ isFollowing: (id: string) => state.following.has(id), toggle: state.toggle }),
}));
vi.mock('src/features/social/VideoComments', () => ({
   default: (props: { videoId: string; signedIn: boolean; locked?: boolean; onTotal?: (n: number) => void }) => (
      <div
         id="comments-stub"
         data-video={props.videoId}
         data-signed-in={String(props.signedIn)}
         data-locked={String(!!props.locked)}
      >
         <button id="comments-stub-total" onClick={() => props.onTotal?.(7)} />
      </div>
   ),
}));
vi.mock('src/services/api/videos', () => api);

import ReelsPage from './ReelsPage';

const video = (id: string, over: Record<string, unknown> = {}) => ({
   videoId: id,
   title: `Title ${id}`,
   status: 'ready',
   priceMinor: 0,
   createdAt: '2026-10-20T10:00:00Z',
   isOwner: false,
   posterUrl: `https://r2.test/${id}.jpg`,
   creator: { creatorId: 'c1', displayName: 'Ada Lovelace', slug: 'ada' },
   ...over,
});
const withLink = (id: string, over: Record<string, unknown> = {}) =>
   video(id, { playbackUrl: `https://r2.test/${id}.mp4`, ...over });

let container: HTMLDivElement;
let root: Root;
let seen: (entries: unknown[]) => void;
const settle = () => act(async () => {});
const players = () => [...document.querySelectorAll<HTMLVideoElement>('.reel-player')];
const reels = () => [...document.querySelectorAll<HTMLElement>('.reel')];
const bringIntoView = (index: number) => act(async () => seen([{ isIntersecting: true, target: reels()[index] }]));
const render = async () => {
   await act(async () => root.render(<ReelsPage />));
   await settle();
};

beforeEach(() => {
   container = document.createElement('div');
   document.body.appendChild(container);
   root = createRoot(container);
   state.user = undefined;
   state.isLoading = false;
   state.startId = undefined;
   state.search = '';
   state.following = new Set();
   (globalThis as { IntersectionObserver?: unknown }).IntersectionObserver = class {
      constructor(callback: (entries: unknown[]) => void) {
         seen = callback;
      }
      observe() {}
      disconnect() {}
   };
   HTMLMediaElement.prototype.play = vi.fn(() => Promise.resolve());
   HTMLMediaElement.prototype.pause = vi.fn();
   Element.prototype.scrollBy = vi.fn();
   api.fetchVideo.mockImplementation((id: string) => Promise.resolve(withLink(id)));
});
afterEach(() => {
   act(() => root.unmount());
   container.remove();
   document.body.innerHTML = '';
   vi.clearAllMocks();
});

test('every video is a screen, only the one in view plays and the next one is made ready', async () => {
   api.fetchFeed.mockResolvedValue({ items: [video('a'), video('b'), video('c'), video('d')], next: null });
   await render();

   expect(reels()).toHaveLength(4);
   expect(players().map((p) => p.getAttribute('src'))).toEqual(['https://r2.test/a.mp4']);
   expect(api.fetchVideo.mock.calls.map((c) => c[0])).toEqual(['a', 'b']);
   expect(HTMLMediaElement.prototype.play).toHaveBeenCalled();
});

test('swiping to the next one moves the playing there and prepares the one after it', async () => {
   api.fetchFeed.mockResolvedValue({ items: [video('a'), video('b'), video('c'), video('d')], next: null });
   await render();

   await bringIntoView(1);
   await settle();

   expect(players().map((p) => p.getAttribute('src'))).toEqual(['https://r2.test/b.mp4']);
   expect(api.fetchVideo.mock.calls.map((c) => c[0])).toEqual(['a', 'b', 'c']);
});

test('a link that opens one video starts with it, and it is not shown twice', async () => {
   state.startId = 'b';
   api.fetchFeed.mockResolvedValue({ items: [video('a'), video('b'), video('c')], next: null });
   await render();

   expect(reels().map((r) => r.querySelector('.reel-title')?.textContent)).toEqual(['Title b', 'Title a', 'Title c']);
   expect(api.fetchVideo).toHaveBeenCalledWith('b', undefined);
   // its link came with it: it is not asked for again
   expect(api.fetchVideo.mock.calls.filter((c) => c[0] === 'b')).toHaveLength(1);
});

test('when the video of the link is gone the latest ones are shown', async () => {
   state.startId = 'gone';
   api.fetchVideo.mockImplementation((id: string) =>
      id === 'gone' ? Promise.reject(new Error('404')) : Promise.resolve(withLink(id)),
   );
   api.fetchFeed.mockResolvedValue({ items: [video('a')], next: null });
   await render();

   expect(reels()).toHaveLength(1);
});

test('a video that costs money is a lock with a way to buy it, and no link is asked for', async () => {
   api.fetchFeed.mockResolvedValue({
      items: [video('a', { locked: true, priceMinor: 250000 }), video('b')],
      next: null,
   });
   await render();

   expect(document.querySelector('.reel-locked')).not.toBeNull();
   expect(document.querySelector('.reel-unlock')?.getAttribute('href')).toBe('/v/a');
   expect(document.querySelector('.reel-unlock')?.textContent).toContain('videos.reels_unlock');
   expect(players()).toHaveLength(0);
   expect(api.fetchVideo.mock.calls.map((c) => c[0])).toEqual(['b']);
});

test('the sound is off to start with and the button turns it on for the videos that follow', async () => {
   api.fetchFeed.mockResolvedValue({ items: [video('a'), video('b')], next: null });
   await render();
   expect(players()[0].muted).toBe(true);

   await act(async () => document.querySelector<HTMLElement>('.reel-mute')?.click());
   expect(players()[0].muted).toBe(false);

   await bringIntoView(1);
   await settle();
   expect(players()[0].muted).toBe(false);
});

const wait = (ms: number) => act(async () => new Promise((resolve) => setTimeout(resolve, ms)));

test('one tap pauses the video (and a tap on a paused video plays it again)', async () => {
   api.fetchFeed.mockResolvedValue({ items: [video('a')], next: null });
   await render();
   const player = players()[0];
   Object.defineProperty(player, 'paused', { value: false, configurable: true });

   await act(async () => player.click());
   // it waits a moment in case the second tap of a double tap follows
   expect(HTMLMediaElement.prototype.pause).not.toHaveBeenCalled();
   await wait(300);

   expect(HTMLMediaElement.prototype.pause).toHaveBeenCalledTimes(1);
});

test('a double tap likes the video, with the heart, and does not pause it', async () => {
   state.user = { access_token: 'tok' };
   api.likeVideo.mockResolvedValue({ likes: 6, liked: true });
   api.fetchFeed.mockResolvedValue({ items: [video('a', { likes: 5, liked: false })], next: null });
   await render();
   const player = players()[0];
   Object.defineProperty(player, 'paused', { value: false, configurable: true });

   await act(async () => player.click());
   await act(async () => player.click());
   await wait(350);

   expect(api.likeVideo).toHaveBeenCalledWith('a');
   expect(document.querySelector('.reel-heart')).not.toBeNull();
   expect(HTMLMediaElement.prototype.pause).not.toHaveBeenCalled();
});

test('a double tap on a video that is liked already does not take the like back', async () => {
   state.user = { access_token: 'tok' };
   api.fetchFeed.mockResolvedValue({ items: [video('a', { likes: 5, liked: true })], next: null });
   await render();

   await act(async () => players()[0].click());
   await act(async () => players()[0].click());
   await wait(350);

   expect(api.likeVideo).not.toHaveBeenCalled();
   expect(api.unlikeVideo).not.toHaveBeenCalled();
});

test('a double tap signed out asks for a sign in', async () => {
   api.fetchFeed.mockResolvedValue({ items: [video('a')], next: null });
   await render();

   await act(async () => players()[0].click());
   await act(async () => players()[0].click());
   await wait(350);

   expect(state.signinRedirect).toHaveBeenCalledWith({ state: { url: '/reels' } });
});

test('near the end the next page of videos is asked for', async () => {
   api.fetchFeed
      .mockResolvedValueOnce({ items: [video('a'), video('b')], next: '2026-10-19T00:00:00Z' })
      .mockResolvedValueOnce({ items: [video('b'), video('c')], next: null });
   await render();

   expect(api.fetchFeed).toHaveBeenLastCalledWith('2026-10-19T00:00:00Z', 12, false);
   expect(reels().map((r) => r.querySelector('.reel-title')?.textContent)).toEqual(['Title a', 'Title b', 'Title c']);
});

test('reporting needs a sign in; a person who is signed in gets the form, the creator gets no button', async () => {
   api.fetchFeed.mockResolvedValue({ items: [video('a'), video('mine', { isOwner: true })], next: null });
   await render();

   expect(document.querySelectorAll('.reel-report')).toHaveLength(1);
   await act(async () => document.querySelector<HTMLElement>('.reel-report')?.click());
   expect(state.signinRedirect).toHaveBeenCalledWith({ state: { url: '/reels' } });
   expect(document.querySelector('#report-dialog')).toBeNull();

   act(() => root.unmount());
   root = createRoot(container);
   state.user = { access_token: 'tok' };
   await render();
   await act(async () => document.querySelector<HTMLElement>('.reel-report')?.click());
   expect(document.querySelector('#report-dialog')?.getAttribute('data-video')).toBe('a');
});

test('sharing copies the link of the video', async () => {
   const writeText = vi.fn(() => Promise.resolve());
   Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
   api.fetchFeed.mockResolvedValue({ items: [video('a')], next: null });
   await render();

   await act(async () => document.querySelector<HTMLElement>('.reel-share')?.click());
   expect(writeText).toHaveBeenCalledWith('https://gbedu.test/v/a');
});

test('nothing is asked for until it is known whether the person is signed in', async () => {
   state.isLoading = true;
   await render();

   expect(api.fetchFeed).not.toHaveBeenCalled();
});

test('with no videos it says so, and when they cannot be loaded it says that', async () => {
   api.fetchFeed.mockResolvedValue({ items: [], next: null });
   await render();
   expect(document.querySelector('#reels-empty')).not.toBeNull();

   act(() => root.unmount());
   root = createRoot(container);
   api.fetchFeed.mockRejectedValue(new Error('down'));
   await render();
   expect(document.querySelector('#reels-failed')).not.toBeNull();
});

test('the follow button follows the creator, or asks for a sign in; the creator has none on their own video', async () => {
   api.fetchFeed.mockResolvedValue({ items: [video('a'), video('mine', { isOwner: true })], next: null });
   await render();
   expect(document.querySelectorAll('.reel-follow')).toHaveLength(1);

   await act(async () => document.querySelector<HTMLElement>('.reel-follow')?.click());
   expect(state.signinRedirect).toHaveBeenCalledWith({ state: { url: '/reels' } });
   expect(state.toggle).not.toHaveBeenCalled();

   act(() => root.unmount());
   root = createRoot(container);
   state.user = { access_token: 'tok' };
   await render();
   await act(async () => document.querySelector<HTMLElement>('.reel-follow')?.click());
   expect(state.toggle).toHaveBeenCalledWith('c1');
});

test('a creator who is followed shows as followed', async () => {
   state.following = new Set(['c1']);
   api.fetchFeed.mockResolvedValue({ items: [video('a')], next: null });
   await render();

   expect(document.querySelector('.reel-follow')?.getAttribute('aria-pressed')).toBe('true');
});

test('with ?following=1 only the videos of the followed creators are asked for, and an empty list says how to fill it', async () => {
   state.search = '?following=1';
   api.fetchFeed.mockResolvedValue({ items: [], next: null });
   await render();

   expect(api.fetchFeed).toHaveBeenCalledWith(undefined, 12, true);
   expect(document.querySelector('#reels-empty')?.textContent).toContain('social.following_empty');
   expect(document.querySelector('#reels-find-creators')?.getAttribute('href')).toBe('/creators');
});

test('the heart shows the likes; pressing it likes the video, or asks for a sign in', async () => {
   api.likeVideo.mockResolvedValue({ likes: 6, liked: true });
   api.fetchFeed.mockResolvedValue({ items: [video('a', { likes: 5, liked: false })], next: null });
   await render();
   expect(document.querySelector('.like-count')?.textContent).toBe('5');

   await act(async () => document.querySelector<HTMLElement>('.like-button')?.click());
   expect(state.signinRedirect).toHaveBeenCalledWith({ state: { url: '/reels' } });
   expect(api.likeVideo).not.toHaveBeenCalled();

   act(() => root.unmount());
   root = createRoot(container);
   state.user = { access_token: 'tok' };
   await render();
   await act(async () => document.querySelector<HTMLElement>('.like-button')?.click());
   await settle();
   expect(api.likeVideo).toHaveBeenCalledWith('a');
   expect(document.querySelector('.like-count')?.textContent).toBe('6');
   expect(document.querySelector('.like-button')?.getAttribute('aria-pressed')).toBe('true');
});

test('the comment button shows the number and opens the comments of that video; what changes there shows on the button', async () => {
   api.fetchFeed.mockResolvedValue({ items: [video('a', { comments: 4 }), video('b')], next: null });
   await render();
   expect(document.querySelector('.reel-comments-count')?.textContent).toBe('4');
   expect(document.querySelector('#comments-stub')).toBeNull();

   await act(async () => document.querySelector<HTMLElement>('.reel-comments')?.click());
   expect(document.querySelector('#comments-stub')?.getAttribute('data-video')).toBe('a');

   await act(async () => document.querySelector<HTMLElement>('#comments-stub-total')?.click());
   expect(document.querySelector('.reel-comments-count')?.textContent).toBe('7');

   await act(async () => document.querySelector<HTMLElement>('#reels-comments-close')?.click());
   await settle();
});

test('the tabs switch between everybody and the creators that are followed; the second needs a sign in', async () => {
   api.fetchFeed.mockResolvedValue({ items: [video('a')], next: null });
   await render();
   expect(document.querySelector('#reels-tab-all')?.getAttribute('aria-pressed')).toBe('true');

   await act(async () => document.querySelector<HTMLElement>('#reels-tab-following')?.click());
   expect(state.signinRedirect).toHaveBeenCalledWith({ state: { url: '/reels?following=1' } });
   expect(state.replace).not.toHaveBeenCalled();

   act(() => root.unmount());
   root = createRoot(container);
   state.user = { access_token: 'tok' };
   await render();
   await act(async () => document.querySelector<HTMLElement>('#reels-tab-following')?.click());
   expect(state.replace).toHaveBeenCalledWith('/reels?following=1');

   state.search = '?following=1';
   act(() => root.unmount());
   root = createRoot(container);
   await render();
   expect(document.querySelector('#reels-tab-following')?.getAttribute('aria-pressed')).toBe('true');
   await act(async () => document.querySelector<HTMLElement>('#reels-tab-all')?.click());
   expect(state.replace).toHaveBeenLastCalledWith('/reels');
});

test('sharing uses the share sheet of the device when there is one, and does not copy', async () => {
   const share = vi.fn(() => Promise.resolve());
   const writeText = vi.fn(() => Promise.resolve());
   Object.defineProperty(navigator, 'share', { value: share, configurable: true });
   Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
   api.fetchFeed.mockResolvedValue({ items: [video('a')], next: null });
   try {
      await render();

      await act(async () => document.querySelector<HTMLElement>('.reel-share')?.click());

      expect(share).toHaveBeenCalledWith(expect.objectContaining({ url: 'https://gbedu.test/v/a' }));
      expect(writeText).not.toHaveBeenCalled();
   } finally {
      Reflect.deleteProperty(navigator, 'share');
   }
});

test('closing the share sheet is not a failure: nothing is copied; copying says so', async () => {
   const abort = Object.assign(new Error('closed'), { name: 'AbortError' });
   const writeText = vi.fn(() => Promise.resolve());
   Object.defineProperty(navigator, 'share', { value: vi.fn(() => Promise.reject(abort)), configurable: true });
   Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
   api.fetchFeed.mockResolvedValue({ items: [video('a')], next: null });
   try {
      await render();
      await act(async () => document.querySelector<HTMLElement>('.reel-share')?.click());
      expect(writeText).not.toHaveBeenCalled();
   } finally {
      Reflect.deleteProperty(navigator, 'share');
   }

   await act(async () => document.querySelector<HTMLElement>('.reel-share')?.click());
   await act(async () => undefined);
   expect(writeText).toHaveBeenCalledWith('https://gbedu.test/v/a');
   expect(document.body.textContent).toContain('videos.link_copied');
});

test('the arrow keys and the buttons go to the next and the previous video; M turns the sound on', async () => {
   api.fetchFeed.mockResolvedValue({ items: [video('a'), video('b')], next: null });
   await render();

   await act(async () => void window.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown' })));
   expect(Element.prototype.scrollBy).toHaveBeenCalledWith(expect.objectContaining({ behavior: 'smooth' }));
   expect(players()[0].muted).toBe(true);
   await act(async () => void window.dispatchEvent(new KeyboardEvent('keydown', { key: 'm' })));
   expect(players()[0].muted).toBe(false);
});

test('the creators page is one tap away', async () => {
   api.fetchFeed.mockResolvedValue({ items: [video('a')], next: null });
   await render();

   expect(document.querySelector('#reels-creators')?.getAttribute('href')).toBe('/creators');
});
