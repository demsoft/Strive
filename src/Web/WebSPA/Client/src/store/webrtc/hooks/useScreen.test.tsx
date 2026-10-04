import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

// one fake "useMedia" state per source ('screen' = the picture, 'screen-audio' = the sound)
type FakeMedia = {
   enabled: boolean;
   getTrack: () => Promise<MediaStreamTrack>;
   enable: ReturnType<typeof vi.fn>;
   disable: ReturnType<typeof vi.fn>;
   pause: ReturnType<typeof vi.fn>;
   resume: ReturnType<typeof vi.fn>;
   switchDevice: ReturnType<typeof vi.fn>;
};
const media: Record<string, FakeMedia> = {};
const dispatch = vi.fn();

vi.mock('./useMedia', () => ({
   default: (source: string, getTrack: () => Promise<MediaStreamTrack>) => {
      const state: FakeMedia = (media[source] ??= {
         enabled: false,
         getTrack,
         enable: vi.fn(async () => {
            await media[source].getTrack();
         }),
         disable: vi.fn(),
         pause: vi.fn(),
         resume: vi.fn(),
         switchDevice: vi.fn(),
      });
      state.getTrack = getTrack;
      return { ...state, connected: true, paused: false };
   },
}));
vi.mock('react-redux', () => ({ useDispatch: () => dispatch }));
vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }) }));

import useScreen from './useScreen';

type Controls = ReturnType<typeof useScreen>;
let controls: Controls;
let container: HTMLDivElement;

function Probe() {
   controls = useScreen();
   return null;
}

const track = (kind: 'video' | 'audio') => ({ kind, readyState: 'live', contentHint: '', getSettings: () => ({}) });
const streamWith = (...kinds: ('video' | 'audio')[]) => {
   const tracks = kinds.map(track);
   return {
      getVideoTracks: () => tracks.filter((x) => x.kind === 'video'),
      getAudioTracks: () => tracks.filter((x) => x.kind === 'audio'),
   };
};

const getDisplayMedia = vi.fn();

beforeEach(() => {
   for (const key of Object.keys(media)) delete media[key];
   dispatch.mockReset();
   getDisplayMedia.mockReset();
   sessionStorage.clear();
   Object.defineProperty(navigator, 'mediaDevices', { value: { getDisplayMedia }, configurable: true });

   container = document.createElement('div');
   document.body.appendChild(container);
   act(() => createRoot(container).render(<Probe />));
});
afterEach(() => container.remove());

test('the screen is requested with its sound, without the processing of a microphone', async () => {
   getDisplayMedia.mockResolvedValue(streamWith('video', 'audio'));

   await act(() => controls.enable());

   const request = getDisplayMedia.mock.calls[0][0];
   expect(request.audio).toEqual({ echoCancellation: false, noiseSuppression: false, autoGainControl: false });
   expect(request.systemAudio).toBe('include');
   expect(media['screen'].enable).toHaveBeenCalledTimes(1);
   expect(media['screen-audio'].enable).toHaveBeenCalledTimes(1);
});

test('a screen with sound is a video that moves, so the frame rate matters', async () => {
   const stream = streamWith('video', 'audio');
   getDisplayMedia.mockResolvedValue(stream);

   await act(() => controls.enable());

   expect(stream.getVideoTracks()[0].contentHint).toBe('motion');
});

test('a screen without sound is shared as before, with a hint (once)', async () => {
   getDisplayMedia.mockResolvedValue(streamWith('video'));

   await act(() => controls.enable());
   await act(() => controls.enable());

   expect(media['screen'].enable).toHaveBeenCalledTimes(2);
   expect(media['screen-audio'].enable).not.toHaveBeenCalled();
   const hints = dispatch.mock.calls.filter(([action]) => action.payload?.message === 'conference.media.screen_no_audio_hint');
   expect(hints).toHaveLength(1);
});

test('when the sound cannot be shared the picture still is', async () => {
   getDisplayMedia.mockResolvedValue(streamWith('video', 'audio'));
   media['screen-audio'].enable.mockRejectedValue(new Error('no transport'));

   await act(() => controls.enable());

   expect(media['screen'].enable).toHaveBeenCalledTimes(1);
});

test('if the browser does not know the sound constraints the screen is requested again without them', async () => {
   getDisplayMedia.mockRejectedValueOnce(new TypeError('unknown constraint')).mockResolvedValueOnce(streamWith('video'));

   await act(() => controls.enable());

   expect(getDisplayMedia).toHaveBeenCalledTimes(2);
   expect(getDisplayMedia.mock.calls[1][0].audio).toBeUndefined();
   expect(media['screen'].enable).toHaveBeenCalledTimes(1);
});

test('cancelling the dialog is not retried', async () => {
   const cancelled = new DOMException('cancelled', 'NotAllowedError');
   getDisplayMedia.mockRejectedValue(cancelled);

   await expect(act(() => controls.enable())).rejects.toBe(cancelled);

   expect(getDisplayMedia).toHaveBeenCalledTimes(1);
   expect(media['screen-audio'].enable).not.toHaveBeenCalled();
});

test('stopping stops the sound first and then the picture', async () => {
   const order: string[] = [];
   media['screen-audio'].disable.mockImplementation(async () => {
      order.push('audio');
   });
   media['screen'].disable.mockImplementation(async () => {
      order.push('video');
   });

   await act(() => controls.disable());

   expect(order).toEqual(['audio', 'video']);
});
