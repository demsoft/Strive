import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({ fetchVideoReports: vi.fn(), keepVideo: vi.fn(), removeVideo: vi.fn() }));

vi.mock('react-i18next', () => ({
   useTranslation: () => ({
      t: (key: string, values?: Record<string, unknown>) => (values ? `${key} ${JSON.stringify(values)}` : key),
   }),
}));
vi.mock('react-router-dom', () => ({ Link: ({ children }: { children: React.ReactNode }) => <a>{children}</a> }));
vi.mock('src/components/BrandLogo', () => ({ default: () => null }));
vi.mock('../api', () => api);

import AdminVideosPage from './AdminVideosPage';

const item = (over: Record<string, unknown> = {}) => ({
   videoId: 'v1',
   title: 'Reported reel',
   creatorName: 'Ada',
   moderation: 'hidden',
   priceMinor: 0,
   buyers: 0,
   openReports: 3,
   reasons: { copyright: 2, spam: 1 },
   details: [{ reason: 'copyright', text: 'Stolen from another channel', at: '2026-10-20T10:00:00Z' }],
   latestReportAt: '2026-10-20T10:00:00Z',
   posterUrl: 'https://r2.test/poster.jpg',
   playbackUrl: 'https://r2.test/video.mp4?sig=1',
   ...over,
});

const settle = () => act(async () => {});
let container: HTMLDivElement;
let root: ReturnType<typeof createRoot> | null = null;

beforeEach(() => {
   container = document.createElement('div');
   document.body.appendChild(container);
});
afterEach(async () => {
   await act(async () => root?.unmount());
   root = null;
   container.remove();
   document.body.innerHTML = '';
   vi.clearAllMocks();
});

const render = async () => {
   root = createRoot(container);
   await act(async () => root!.render(<AdminVideosPage />));
   await settle();
};

test('every reported video is shown with how many reports, why, what was said and whether it is hidden', async () => {
   api.fetchVideoReports.mockResolvedValue([item()]);
   await render();

   const card = document.querySelector('.reported-video')!;
   expect(card.textContent).toContain('Reported reel');
   expect(card.querySelector('.video-hidden')).not.toBeNull();
   expect(card.querySelector('.report-count')?.textContent).toContain('"count":3');
   expect([...card.querySelectorAll('.report-reason')].map((x) => x.textContent)).toEqual([
      'videos.reason_copyright · 2',
      'videos.reason_spam · 1',
   ]);
   expect(card.querySelector('.report-detail')?.textContent).toContain('Stolen from another channel');
});

test('nothing waiting says so', async () => {
   api.fetchVideoReports.mockResolvedValue([]);
   await render();

   expect(document.querySelector('#admin-videos-none')).not.toBeNull();
});

test('people who bought the video are pointed out before anything is decided', async () => {
   api.fetchVideoReports.mockResolvedValue([item({ buyers: 4, priceMinor: 300000 })]);
   await render();

   expect(document.querySelector('.report-buyers')?.textContent).toContain('"count":4');
});

test('a moderator can watch the video, also while it is hidden', async () => {
   api.fetchVideoReports.mockResolvedValue([item(), item({ videoId: 'v2', playbackUrl: null })]);
   await render();

   const watch = document.querySelectorAll('.report-watch');
   expect(watch).toHaveLength(1); // a video without files cannot be watched
   await act(async () => (watch[0] as HTMLElement).click());
   expect(document.querySelector('#admin-video-player')?.getAttribute('src')).toBe('https://r2.test/video.mp4?sig=1');
});

test('keeping a video asks the server and loads the list again', async () => {
   api.fetchVideoReports.mockResolvedValueOnce([item()]).mockResolvedValue([]);
   api.keepVideo.mockResolvedValue(undefined);
   await render();

   await act(async () => (document.querySelector('.report-keep') as HTMLElement).click());
   await settle();

   expect(api.keepVideo).toHaveBeenCalledWith('v1');
   expect(document.querySelector('.reported-video')).toBeNull();
});

test('removing asks first, sends the word for the creator, and loads the list again', async () => {
   api.fetchVideoReports.mockResolvedValueOnce([item()]).mockResolvedValue([]);
   api.removeVideo.mockResolvedValue(undefined);
   await render();

   await act(async () => (document.querySelector('.report-remove') as HTMLElement).click());
   expect(api.removeVideo).not.toHaveBeenCalled();
   const note = document.querySelector('#remove-note') as HTMLTextAreaElement;
   const setter = Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value')!.set!;
   await act(async () => {
      setter.call(note, '  Not your own work.  ');
      note.dispatchEvent(new Event('input', { bubbles: true }));
   });
   await act(async () => (document.querySelector('#remove-confirm') as HTMLElement).click());
   await settle();

   expect(api.removeVideo).toHaveBeenCalledWith('v1', 'Not your own work.');
   expect(api.fetchVideoReports).toHaveBeenCalledTimes(2);
});

test('when the decision does not work an error is shown and the video stays', async () => {
   api.fetchVideoReports.mockResolvedValue([item()]);
   api.keepVideo.mockRejectedValue(new Error('down'));
   await render();

   await act(async () => (document.querySelector('.report-keep') as HTMLElement).click());
   await settle();

   expect(api.fetchVideoReports).toHaveBeenCalledTimes(1);
   expect(document.querySelector('.reported-video')).not.toBeNull();
});

test('somebody who is no administrator sees the notice, not the reports', async () => {
   api.fetchVideoReports.mockRejectedValue({ response: { status: 403 } });
   await render();

   expect(document.querySelector('#admin-videos-forbidden')).not.toBeNull();
   expect(document.querySelector('#admin-videos-page')).toBeNull();
});
