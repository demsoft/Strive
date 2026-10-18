import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const media = {
   source: '',
   getTrack: undefined as undefined | ((deviceId?: string) => Promise<MediaStreamTrack>),
   options: undefined as unknown,
};

vi.mock('./useMedia', () => ({
   default: (source: string, getTrack: (deviceId?: string) => Promise<MediaStreamTrack>, options: unknown) => {
      media.source = source;
      media.getTrack = getTrack;
      media.options = options;
      return { enable: vi.fn(), enabled: false };
   },
}));

import useInstrument from './useInstrument';

const getUserMedia = vi.fn();
const track = {};
let container: HTMLDivElement;

function Probe() {
   useInstrument();
   return null;
}

beforeEach(() => {
   getUserMedia.mockReset().mockResolvedValue({ getAudioTracks: () => [track], getTracks: () => [track] });
   Object.defineProperty(navigator, 'mediaDevices', { value: { getUserMedia }, configurable: true });
   container = document.createElement('div');
   document.body.appendChild(container);
   act(() => createRoot(container).render(<Probe />));
});
afterEach(() => container.remove());

test('is its own producer source next to the microphone, in stereo', () => {
   expect(media.source).toBe('instrument');
   expect(media.options).toMatchObject({ codecOptions: { opusStereo: true, opusDtx: false } });
});

test('asks the chosen sound card input for raw sound without processing', async () => {
   await media.getTrack!('focusrite-1');

   const { audio } = getUserMedia.mock.calls[0][0];
   expect(audio).toMatchObject({
      deviceId: { exact: 'focusrite-1' },
      echoCancellation: false,
      noiseSuppression: false,
      autoGainControl: false,
      channelCount: 2,
   });
});

test('gives the track of the input, stopping it releases the input', async () => {
   expect(await media.getTrack!()).toBe(track);
});
