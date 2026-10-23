import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({ fetchFeed: vi.fn() }));

const auth = vi.hoisted(() => ({ user: undefined as undefined | { access_token: string } }));
vi.mock('react-oidc-context', () => ({ useAuth: () => ({ user: auth.user }) }));
vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }) }));
vi.mock('./VideoTile', () => ({
   default: ({ video }: { video: { videoId: string } }) => <div className="tile" data-id={video.videoId} />,
}));
vi.mock('react-router-dom', () => ({
   Link: React.forwardRef<HTMLAnchorElement, { to: string; children: React.ReactNode; id?: string }>(
      ({ to, children, id }, ref) => (
         <a ref={ref} href={to} id={id}>
            {children}
         </a>
      ),
   ),
}));
vi.mock('src/services/api/videos', () => api);

import DiscoverVideos from './DiscoverVideos';

const video = (id: string) => ({
   videoId: id,
   title: id,
   status: 'ready',
   priceMinor: 0,
   createdAt: '',
   isOwner: false,
});
const ids = () => [...document.querySelectorAll('.tile')].map((x) => x.getAttribute('data-id'));
const settle = () => act(async () => {});

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

test('shows the latest videos, with no more button when that is all', async () => {
   api.fetchFeed.mockResolvedValue({ items: [video('a'), video('b')], next: null });
   await act(async () => createRoot(container).render(<DiscoverVideos />));
   await settle();

   expect(ids()).toEqual(['a', 'b']);
   expect(document.querySelector('#discover-videos-more')).toBeNull();
});

test('more videos are added below, and one that is already shown is not shown twice', async () => {
   api.fetchFeed
      .mockResolvedValueOnce({ items: [video('a'), video('b')], next: '2026-10-20T10:00:00Z' })
      .mockResolvedValueOnce({ items: [video('b'), video('c')], next: null });
   await act(async () => createRoot(container).render(<DiscoverVideos />));
   await settle();

   await act(async () => (document.querySelector('#discover-videos-more') as HTMLElement).click());
   await settle();

   expect(api.fetchFeed).toHaveBeenLastCalledWith('2026-10-20T10:00:00Z', 12, false);
   expect(ids()).toEqual(['a', 'b', 'c']);
   expect(document.querySelector('#discover-videos-more')).toBeNull();
});

test('while there are no videos nothing is shown at all', async () => {
   api.fetchFeed.mockResolvedValue({ items: [], next: null });
   await act(async () => createRoot(container).render(<DiscoverVideos />));
   await settle();

   expect(document.querySelector('#discover-videos')).toBeNull();
});

test('when the videos cannot be loaded a message is shown, not silence', async () => {
   api.fetchFeed.mockRejectedValue(new Error('down'));
   await act(async () => createRoot(container).render(<DiscoverVideos />));
   await settle();

   expect(document.querySelector('#discover-videos-failed')).not.toBeNull();
});

test('leads to the reels when there are videos, and shows no button when there are none', async () => {
   api.fetchFeed.mockResolvedValue({ items: [video('a')], next: null });
   const root = createRoot(container);
   await act(async () => root.render(<DiscoverVideos />));
   await settle();
   expect(document.querySelector('#discover-videos-reels')?.getAttribute('href')).toBe('/reels');
   act(() => root.unmount());
});

const tab = (name: 'all' | 'following') => document.querySelector<HTMLElement>(`#discover-videos-tab-${name}`);

test('a visitor has no tabs; a signed in person can switch to the creators they follow', async () => {
   api.fetchFeed.mockResolvedValue({ items: [video('a')], next: null });
   const root = createRoot(container);
   await act(async () => root.render(<DiscoverVideos />));
   await settle();
   expect(tab('following')).toBeNull();
   act(() => root.unmount());

   auth.user = { access_token: 'tok' };
   const signedIn = createRoot(container);
   await act(async () => signedIn.render(<DiscoverVideos />));
   await settle();
   api.fetchFeed.mockResolvedValue({ items: [video('x'), video('y')], next: null });
   await act(async () => tab('following')?.click());
   await settle();

   expect(api.fetchFeed).toHaveBeenLastCalledWith(undefined, 12, true);
   expect(ids()).toEqual(['x', 'y']);
   // the reels follow the tab
   expect(document.querySelector('#discover-videos-reels')?.getAttribute('href')).toBe('/reels?following=1');

   await act(async () => tab('all')?.click());
   await settle();
   expect(api.fetchFeed).toHaveBeenLastCalledWith(undefined, 12, false);
   act(() => signedIn.unmount());
});

test('when nobody the person follows has videos the tab says how to get some, and stays', async () => {
   auth.user = { access_token: 'tok' };
   api.fetchFeed.mockResolvedValueOnce({ items: [video('a')], next: null });
   const root = createRoot(container);
   await act(async () => root.render(<DiscoverVideos />));
   await settle();
   api.fetchFeed.mockResolvedValue({ items: [], next: null });
   await act(async () => tab('following')?.click());
   await settle();

   expect(document.querySelector('#discover-videos-following-empty')).not.toBeNull();
   expect(tab('all')).not.toBeNull();
   act(() => root.unmount());
});
