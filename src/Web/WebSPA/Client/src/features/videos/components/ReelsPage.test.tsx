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
   useParams: () => ({ videoId: state.startId }),
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

test('tapping the video pauses it and plays it again', async () => {
   api.fetchFeed.mockResolvedValue({ items: [video('a')], next: null });
   await render();
   const player = players()[0];
   Object.defineProperty(player, 'paused', { value: false, configurable: true });

   await act(async () => player.click());
   expect(HTMLMediaElement.prototype.pause).toHaveBeenCalled();
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
   expect(document.querySelector('#reels-empty')?.textContent).toBe('social.following_empty');
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
