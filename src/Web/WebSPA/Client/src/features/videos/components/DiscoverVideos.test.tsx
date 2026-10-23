import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({ fetchFeed: vi.fn() }));

vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }) }));
vi.mock('./VideoTile', () => ({
   default: ({ video }: { video: { videoId: string } }) => <div className="tile" data-id={video.videoId} />,
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

   expect(api.fetchFeed).toHaveBeenLastCalledWith('2026-10-20T10:00:00Z');
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
