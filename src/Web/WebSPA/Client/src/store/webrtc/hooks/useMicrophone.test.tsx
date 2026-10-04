import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const media = { getTrack: undefined as undefined | ((deviceId?: string) => Promise<MediaStreamTrack>) };

vi.mock('./useMedia', () => ({
   default: (_source: string, getTrack: (deviceId?: string) => Promise<MediaStreamTrack>) => {
      media.getTrack = getTrack;
      return { enable: vi.fn(), enabled: false };
   },
}));

import useMicrophone from './useMicrophone';

function Probe() {
   useMicrophone();
   return null;
}

const contexts: FakeAudioContext[] = [];
class FakeAudioContext {
   state = 'suspended';
   resume = vi.fn(async () => {
      this.state = 'running';
   });
   close = vi.fn(async () => undefined);
   originalStop = vi.fn();
   destinationTrack = { kind: 'audio', stop: this.originalStop };
   constructor() {
      contexts.push(this);
   }
   createGain() {
      return { gain: { value: 1 }, connect: vi.fn() };
   }
   createMediaStreamSource() {
      return { connect: vi.fn() };
   }
   createMediaStreamDestination() {
      return { stream: { getAudioTracks: () => [this.destinationTrack] } };
   }
}

let container: HTMLDivElement;
const sourceTrack = { stop: vi.fn() };
const getUserMedia = vi.fn();

beforeEach(() => {
   contexts.length = 0;
   sourceTrack.stop.mockReset();
   getUserMedia.mockReset().mockResolvedValue({ getTracks: () => [sourceTrack] });
   vi.stubGlobal('AudioContext', FakeAudioContext);
   Object.defineProperty(navigator, 'mediaDevices', { value: { getUserMedia }, configurable: true });
   container = document.createElement('div');
   document.body.appendChild(container);
   act(() => createRoot(container).render(<Probe />));
});
afterEach(() => {
   container.remove();
   vi.unstubAllGlobals();
});

test('the audio context is woken up (an iPhone starts it suspended, the microphone would be silent)', async () => {
   await media.getTrack!();

   expect(contexts).toHaveLength(1);
   expect(contexts[0].resume).toHaveBeenCalled();
   expect(contexts[0].state).toBe('running');
});

test('the context is woken up before the permission is awaited, while the tap is still being handled', async () => {
   const order: string[] = [];
   getUserMedia.mockImplementation(async () => {
      order.push('getUserMedia');
      return { getTracks: () => [sourceTrack] };
   });
   const original = FakeAudioContext.prototype.createGain;
   FakeAudioContext.prototype.createGain = function () {
      return original.call(this);
   };
   vi.stubGlobal(
      'AudioContext',
      class extends FakeAudioContext {
         constructor() {
            super();
            this.resume = vi.fn(async () => {
               order.push('resume');
            });
         }
      },
   );

   await media.getTrack!();

   expect(order[0]).toBe('resume');
   expect(order).toContain('getUserMedia');
});

test('stopping the track releases the real microphone and closes the audio context', async () => {
   const track = (await media.getTrack!()) as unknown as { stop: () => void };

   track.stop();

   expect(contexts[0].originalStop).toHaveBeenCalled();
   expect(sourceTrack.stop).toHaveBeenCalled();
   expect(contexts[0].close).toHaveBeenCalled();
});

test('when the microphone is refused the audio context is closed again', async () => {
   getUserMedia.mockRejectedValue(new DOMException('no', 'NotAllowedError'));

   await expect(media.getTrack!()).rejects.toThrow();

   expect(contexts[0].close).toHaveBeenCalled();
});
