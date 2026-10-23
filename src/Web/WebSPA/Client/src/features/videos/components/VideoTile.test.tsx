import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test } from 'vitest';
import { vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

vi.mock('react-router-dom', () => ({
   Link: React.forwardRef<HTMLAnchorElement, { to: string; children: React.ReactNode; className?: string }>(
      ({ to, children, className }, ref) => (
         <a ref={ref} href={to} className={className}>
            {children}
         </a>
      ),
   ),
}));

import VideoTile from './VideoTile';

const video = (over: Record<string, unknown> = {}) =>
   ({
      videoId: 'v1',
      title: 'First reel',
      status: 'ready',
      priceMinor: 0,
      createdAt: '2026-10-20T10:00:00Z',
      isOwner: false,
      durationSeconds: 95,
      posterUrl: 'https://r2.test/poster.jpg',
      creator: { creatorId: 'c1', displayName: 'Ada Lovelace', photoVersion: '5' },
      ...over,
   } as any);

let container: HTMLDivElement;
beforeEach(() => {
   container = document.createElement('div');
   document.body.appendChild(container);
});
afterEach(() => {
   container.remove();
   document.body.innerHTML = '';
});

test('a tile leads to the page of the video and shows its title, length and creator', async () => {
   await act(async () => createRoot(container).render(<VideoTile video={video()} />));

   const tile = container.querySelector('.video-tile')!;
   expect(tile.getAttribute('href')).toBe('/v/v1');
   expect(tile.querySelector('.video-title')?.textContent).toBe('First reel');
   expect(tile.querySelector('.video-duration')?.textContent).toBe('1:35');
   expect(tile.textContent).toContain('Ada Lovelace');
});

test('without a length or a creator those parts are left out', async () => {
   await act(async () =>
      createRoot(container).render(<VideoTile video={video({ durationSeconds: null, creator: null })} />),
   );

   expect(container.querySelector('.video-duration')).toBeNull();
   expect(container.textContent).toContain('First reel');
});
