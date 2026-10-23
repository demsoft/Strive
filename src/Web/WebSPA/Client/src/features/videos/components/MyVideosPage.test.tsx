import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => {
   class UploadError extends Error {}
   return {
      UploadError,
      createVideo: vi.fn(),
      uploadToConverter: vi.fn(),
      completeUpload: vi.fn(),
      fetchMyVideos: vi.fn(),
      fetchVideo: vi.fn(),
      deleteVideo: vi.fn(),
   };
});
const file = vi.hoisted(() => ({ readDuration: vi.fn() }));

vi.mock('react-i18next', () => ({
   useTranslation: () => ({
      t: (key: string, values?: Record<string, unknown>) => (values ? `${key} ${JSON.stringify(values)}` : key),
   }),
}));
vi.mock('react-router-dom', () => ({ Link: ({ children }: { children: React.ReactNode }) => <a>{children}</a> }));
vi.mock('src/components/BrandLogo', () => ({ default: () => null }));
vi.mock('src/services/api/videos', () => api);
vi.mock('src/features/creator/errors', () => ({
   requestErrorMessage: (e: unknown) => String((e as Error).message ?? e),
}));
vi.mock('../videoFile', async (original) => ({ ...(await original<typeof import('../videoFile')>()), ...file }));

import MyVideosPage from './MyVideosPage';

const video = (over: Record<string, unknown> = {}) => ({
   videoId: 'v1',
   title: 'First reel',
   status: 'ready',
   priceMinor: 0,
   createdAt: '2026-10-20T10:00:00Z',
   isOwner: true,
   durationSeconds: 65,
   posterUrl: 'https://r2.test/poster.jpg',
   ...over,
});

const settle = () => act(async () => {});
const type = async (selector: string, value: string) => {
   const input = document.querySelector(selector) as HTMLInputElement | HTMLTextAreaElement;
   const proto = input instanceof HTMLTextAreaElement ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
   const setter = Object.getOwnPropertyDescriptor(proto, 'value')!.set!;
   await act(async () => {
      setter.call(input, value);
      input.dispatchEvent(new Event('input', { bubbles: true }));
   });
};
const choose = async (chosen: File) => {
   const input = document.querySelector('#video-input') as HTMLInputElement;
   Object.defineProperty(input, 'files', { value: [chosen], configurable: true });
   await act(async () => {
      input.dispatchEvent(new Event('change', { bubbles: true }));
   });
};
const sendButton = () => document.querySelector('#video-send') as HTMLButtonElement;
const mp4 = (size = 5_000_000) => new File([new Uint8Array(10)], 'my-first_reel.mp4', { type: 'video/mp4' });

let container: HTMLDivElement;
beforeEach(() => {
   container = document.createElement('div');
   document.body.appendChild(container);
   api.fetchMyVideos.mockResolvedValue([]);
   file.readDuration.mockResolvedValue(60);
});
afterEach(() => {
   container.remove();
   document.body.innerHTML = '';
   vi.clearAllMocks();
   vi.useRealTimers();
});

const render = async () => {
   await act(async () => createRoot(container).render(<MyVideosPage />));
   await settle();
};

test('nothing can be sent without a file and a title; the name of the file suggests a title', async () => {
   await render();
   expect(sendButton().disabled).toBe(true);
   expect(document.querySelector('#no-videos')).not.toBeNull();

   await choose(mp4());
   expect((document.querySelector('#video-title') as HTMLInputElement).value).toBe('my first reel');
   expect(sendButton().disabled).toBe(false);

   await type('#video-title', 'ab');
   expect(sendButton().disabled).toBe(true);
});

test('a file that is no video, too big or too long is turned away at once, with the reason', async () => {
   await render();

   await choose(new File(['x'], 'pic.png', { type: 'image/png' }));
   expect(document.querySelector('#video-error')?.textContent).toContain('videos.problem_not_video');

   file.readDuration.mockResolvedValue(301);
   await choose(mp4());
   expect(document.querySelector('#video-error')?.textContent).toContain('videos.problem_too_long');
   expect(sendButton().disabled).toBe(true);
});

test('sending: announce, send the file with progress, then say that it arrived', async () => {
   const order: string[] = [];
   const ticket = { url: 'https://converter.test/upload', fields: { a: '1' } };
   api.createVideo.mockImplementation(async () => {
      order.push('create');
      return { video: video({ status: 'uploading' }), ticket };
   });
   api.uploadToConverter.mockImplementation((_ticket, _file, onProgress) => {
      order.push('upload');
      onProgress(0.5);
      return { promise: Promise.resolve(), abort: vi.fn() };
   });
   api.completeUpload.mockImplementation(async () => {
      order.push('complete');
      return video({ status: 'processing' });
   });
   await render();
   const chosen = mp4();
   await choose(chosen);
   await type('#video-title', 'My reel');
   await type('#video-description', 'About piano');

   await act(async () => sendButton().click());
   await settle();

   expect(order).toEqual(['create', 'upload', 'complete']);
   expect(api.createVideo).toHaveBeenCalledWith('My reel', 'About piano', chosen.size);
   expect(api.uploadToConverter).toHaveBeenCalledWith(ticket, chosen, expect.any(Function));
   expect((document.querySelector('#video-title') as HTMLInputElement).value).toBe('');
   expect(document.querySelector('#video-error')).toBeNull();
   expect(api.fetchMyVideos).toHaveBeenCalledTimes(2); // at the start and after sending
});

test('the converter refuses the file: its message is shown and the form stays usable', async () => {
   api.createVideo.mockResolvedValue({ video: video({ status: 'uploading' }), ticket: { url: 'u', fields: {} } });
   api.uploadToConverter.mockImplementation(() => ({
      promise: Promise.reject(new api.UploadError('File size too large. Maximum is 104857600.')),
      abort: vi.fn(),
   }));
   await render();
   await choose(mp4());
   await type('#video-title', 'My reel');

   await act(async () => sendButton().click());
   await settle();

   expect(document.querySelector('#video-error')?.textContent).toContain('File size too large');
   expect(api.completeUpload).not.toHaveBeenCalled();
   expect(sendButton().disabled).toBe(false);
});

test('a lost connection without a message gets the friendly one; the server saying no is shown as it is', async () => {
   api.createVideo.mockRejectedValueOnce(new Error('Videos are not available yet.'));
   await render();
   await choose(mp4());
   await type('#video-title', 'My reel');
   await act(async () => sendButton().click());
   await settle();
   expect(document.querySelector('#video-error')?.textContent).toContain('Videos are not available yet.');

   api.createVideo.mockResolvedValue({ video: video(), ticket: { url: 'u', fields: {} } });
   api.uploadToConverter.mockImplementation(() => ({
      promise: Promise.reject(new api.UploadError('')),
      abort: vi.fn(),
   }));
   await act(async () => sendButton().click());
   await settle();
   expect(document.querySelector('#video-error')?.textContent).toContain('videos.upload_failed');
});

test('the list shows every video with its state, and why one failed', async () => {
   api.fetchMyVideos.mockResolvedValue([
      video({ videoId: 'a', status: 'ready' }),
      video({ videoId: 'b', status: 'processing', posterUrl: null }),
      video({ videoId: 'c', status: 'failed', failureReason: 'The video is longer than 5 minutes.', posterUrl: null }),
   ]);
   await render();

   const rows = document.querySelectorAll('.my-video');
   expect(rows).toHaveLength(3);
   expect(rows[0].querySelector('.video-status-ready')).not.toBeNull();
   expect(rows[0].querySelector('.video-watch')).not.toBeNull();
   expect(rows[1].querySelector('.video-status-processing')).not.toBeNull();
   expect(rows[1].querySelector('.video-watch')).toBeNull();
   expect(rows[2].querySelector('.video-failure')?.textContent).toContain('longer than 5 minutes');
});

test('while a video is being prepared the list is asked again until it is done', async () => {
   vi.useFakeTimers();
   api.fetchMyVideos
      .mockResolvedValueOnce([video({ status: 'processing' })])
      .mockResolvedValueOnce([video({ status: 'processing' })])
      .mockResolvedValue([video({ status: 'ready' })]);
   await render();
   expect(api.fetchMyVideos).toHaveBeenCalledTimes(1);

   await act(async () => vi.advanceTimersByTimeAsync(8000));
   expect(api.fetchMyVideos).toHaveBeenCalledTimes(2);
   await act(async () => vi.advanceTimersByTimeAsync(8000));
   expect(api.fetchMyVideos).toHaveBeenCalledTimes(3);

   // it is ready: no more asking
   await act(async () => vi.advanceTimersByTimeAsync(30000));
   expect(api.fetchMyVideos).toHaveBeenCalledTimes(3);
});

test('watching asks for a fresh link and plays it', async () => {
   api.fetchMyVideos.mockResolvedValue([video()]);
   api.fetchVideo.mockResolvedValue(
      video({ playbackUrl: 'https://r2.test/video.mp4?sig=1', description: 'About piano' }),
   );
   await render();

   await act(async () => (document.querySelector('.video-watch') as HTMLElement).click());
   await settle();

   expect(api.fetchVideo).toHaveBeenCalledWith('v1');
   expect(document.querySelector('#video-player')?.getAttribute('src')).toBe('https://r2.test/video.mp4?sig=1');
});

test('deleting asks first, then removes the video and reloads', async () => {
   api.fetchMyVideos.mockResolvedValue([video()]);
   api.deleteVideo.mockResolvedValue(undefined);
   const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
   await render();

   await act(async () => (document.querySelector('.video-delete') as HTMLElement).click());
   expect(api.deleteVideo).not.toHaveBeenCalled();

   confirm.mockReturnValue(true);
   await act(async () => (document.querySelector('.video-delete') as HTMLElement).click());
   await settle();
   expect(api.deleteVideo).toHaveBeenCalledWith('v1');
   expect(api.fetchMyVideos).toHaveBeenCalledTimes(2);
});

test('the list cannot be loaded: the error is shown, not an empty list', async () => {
   api.fetchMyVideos.mockRejectedValue(new Error('down'));
   await render();

   expect(document.querySelector('#my-videos-page')?.textContent).toContain('down');
   expect(document.querySelector('#no-videos')).toBeNull();
});
