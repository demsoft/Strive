import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({
   fetchVideo: vi.fn(),
   videoLink: (id: string) => `https://gbedu.test/v/${id}`,
}));
const state = vi.hoisted(() => ({ user: undefined as undefined | { access_token: string }, isLoading: false }));

vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }) }));
vi.mock('react-oidc-context', () => ({ useAuth: () => ({ user: state.user, isLoading: state.isLoading }) }));
vi.mock('react-router-dom', () => ({
   Link: React.forwardRef<HTMLAnchorElement, { to: string; children: React.ReactNode; id?: string }>(
      ({ to, children, id }, ref) => (
         <a ref={ref} href={to} id={id}>
            {children}
         </a>
      ),
   ),
   useParams: () => ({ videoId: 'v1' }),
}));
vi.mock('src/components/BrandLogo', () => ({ default: () => null }));
vi.mock('src/services/api/videos', () => api);

import WatchVideoPage from './WatchVideoPage';

const video = (over: Record<string, unknown> = {}) => ({
   videoId: 'v1',
   title: 'First reel',
   description: 'About piano',
   status: 'ready',
   priceMinor: 0,
   createdAt: '2026-10-20T10:00:00Z',
   isOwner: false,
   playbackUrl: 'https://r2.test/video.mp4?sig=1',
   posterUrl: 'https://r2.test/poster.jpg',
   creator: { creatorId: 'c1', displayName: 'Ada Lovelace', slug: 'ada' },
   ...over,
});

const settle = () => act(async () => {});

let container: HTMLDivElement;
beforeEach(() => {
   container = document.createElement('div');
   document.body.appendChild(container);
   state.user = undefined;
   state.isLoading = false;
});
afterEach(() => {
   container.remove();
   document.body.innerHTML = '';
   vi.clearAllMocks();
});

const render = async () => {
   await act(async () => createRoot(container).render(<WatchVideoPage />));
   await settle();
};

test('the video plays, with its title, text and a link to the creator (by name when there is one)', async () => {
   api.fetchVideo.mockResolvedValue(video());
   await render();

   expect(api.fetchVideo).toHaveBeenCalledWith('v1', undefined);
   expect(document.querySelector('#video-player')?.getAttribute('src')).toBe('https://r2.test/video.mp4?sig=1');
   expect(document.querySelector('#watch-title')?.textContent).toBe('First reel');
   expect(document.querySelector('#watch-description')?.textContent).toBe('About piano');
   expect(document.querySelector('#watch-creator')?.getAttribute('href')).toBe('/u/ada');
});

test('a person who is signed in is asked for as themselves (the creator sees their own before it is ready)', async () => {
   state.user = { access_token: 'tok' };
   api.fetchVideo.mockResolvedValue(video());
   await render();

   expect(api.fetchVideo).toHaveBeenCalledWith('v1', 'tok');
});

test('nothing is asked for until it is known whether the person is signed in', async () => {
   state.isLoading = true;
   await render();

   expect(api.fetchVideo).not.toHaveBeenCalled();
});

test('a video that cannot be played yet says so instead of an empty player', async () => {
   api.fetchVideo.mockResolvedValue(video({ status: 'processing', playbackUrl: null }));
   await render();

   expect(document.querySelector('#video-player')).toBeNull();
   expect(document.querySelector('#watch-not-ready')).not.toBeNull();
});

test('an unknown video shows the not found message', async () => {
   api.fetchVideo.mockRejectedValue(new Error('404'));
   await render();

   expect(document.querySelector('#watch-not-found')).not.toBeNull();
   expect(document.querySelector('#video-player')).toBeNull();
});

test('the share button copies the link of the video', async () => {
   api.fetchVideo.mockResolvedValue(video());
   const writeText = vi.fn().mockResolvedValue(undefined);
   Object.assign(navigator, { clipboard: { writeText } });
   await render();

   await act(async () => (document.querySelector('#watch-share') as HTMLElement).click());

   expect(writeText).toHaveBeenCalledWith('https://gbedu.test/v/v1');
});
