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

const gainNodes: { gain: { value: number }; connect: ReturnType<typeof vi.fn> }[] = [];
const contexts: { close: ReturnType<typeof vi.fn>; destination: object }[] = [];
class FakeAudioContext {
   destination = {};
   resume = vi.fn(async () => undefined);
   close = vi.fn(async () => undefined);
   constructor() {
      contexts.push(this);
   }
   createGain() {
      const node = { gain: { value: 1 }, connect: vi.fn() };
      gainNodes.push(node);
      return node;
   }
   createMediaStreamSource() {
      return { connect: vi.fn() };
   }
}

const getUserMedia = vi.fn();
const stop = vi.fn();
let track: { stop: () => void };
let container: HTMLDivElement;
let root: ReturnType<typeof createRoot>;

function Probe({ monitor }: { monitor: boolean }) {
   useInstrument(monitor);
   return null;
}
const render = (monitor: boolean) => act(() => root.render(<Probe monitor={monitor} />));

beforeEach(() => {
   gainNodes.length = 0;
   contexts.length = 0;
   stop.mockReset();
   track = { stop };
   getUserMedia.mockReset().mockResolvedValue({ getAudioTracks: () => [track], getTracks: () => [track] });
   vi.stubGlobal('AudioContext', FakeAudioContext);
   Object.defineProperty(navigator, 'mediaDevices', { value: { getUserMedia }, configurable: true });
   container = document.createElement('div');
   document.body.appendChild(container);
   root = createRoot(container);
   render(false);
});
afterEach(() => {
   container.remove();
   vi.unstubAllGlobals();
});

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

test('the monitor is silent by default and plays to the headphones when switched on, while playing', async () => {
   await media.getTrack!();
   expect(gainNodes[0].gain.value).toBe(0);
   expect(gainNodes[0].connect).toHaveBeenCalledWith(contexts[0].destination);

   render(true);
   expect(gainNodes[0].gain.value).toBe(1);

   render(false);
   expect(gainNodes[0].gain.value).toBe(0);
});

test('stopping the track releases the input and ends the monitor', async () => {
   const result = (await media.getTrack!()) as unknown as { stop: () => void };

   result.stop();

   expect(stop).toHaveBeenCalledTimes(1);
   expect(contexts[0].close).toHaveBeenCalled();
});

test('when the input cannot be opened the audio context is closed again', async () => {
   getUserMedia.mockRejectedValue(new DOMException('no', 'NotFoundError'));

   await expect(media.getTrack!()).rejects.toThrow();

   expect(contexts[0].close).toHaveBeenCalled();
});
