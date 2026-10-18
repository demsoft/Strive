import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

import useInstrumentMonitor, { InstrumentMonitorStatus } from './useInstrumentMonitor';

const contexts: FakeAudioContext[] = [];
let startState: 'running' | 'suspended' = 'running';
/** a browser ignores resume() until the person has clicked on the page */
let resumeAllowed = true;
class FakeAudioContext {
   state: string = startState;
   baseLatency = 0.005;
   outputLatency = 0.024;
   destination = {};
   sources: { connect: ReturnType<typeof vi.fn> }[] = [];
   private listeners: Array<() => void> = [];
   resume = vi.fn(async () => {
      if (!resumeAllowed) return;
      this.state = 'running';
      this.listeners.forEach((x) => x());
   });
   close = vi.fn(async () => undefined);
   constructor() {
      contexts.push(this);
   }
   addEventListener(_type: string, listener: () => void) {
      this.listeners.push(listener);
   }
   createMediaStreamSource() {
      const node = { connect: vi.fn() };
      this.sources.push(node);
      return node;
   }
}

const getUserMedia = vi.fn();
const trackStop = vi.fn();
let latest: InstrumentMonitorStatus;
let container: HTMLDivElement;
let root: ReturnType<typeof createRoot>;

function Probe({ enabled, deviceId }: { enabled: boolean; deviceId?: string }) {
   latest = useInstrumentMonitor(enabled, deviceId);
   return null;
}
const render = async (enabled: boolean, deviceId?: string) =>
   act(async () => {
      root.render(<Probe enabled={enabled} deviceId={deviceId} />);
   });

beforeEach(() => {
   contexts.length = 0;
   startState = 'running';
   resumeAllowed = true;
   trackStop.mockReset();
   getUserMedia.mockReset().mockImplementation(async () => {
      const track = { stop: trackStop, addEventListener: vi.fn() };
      return { getAudioTracks: () => [track], getTracks: () => [track] };
   });
   vi.stubGlobal('AudioContext', FakeAudioContext);
   Object.defineProperty(navigator, 'mediaDevices', { value: { getUserMedia }, configurable: true });
   container = document.createElement('div');
   document.body.appendChild(container);
   root = createRoot(container);
});
afterEach(() => {
   act(() => root.unmount());
   container.remove();
   vi.unstubAllGlobals();
});

test('off: nothing is opened', async () => {
   await render(false, 'v8s');

   expect(latest).toEqual({ state: 'off' });
   expect(getUserMedia).not.toHaveBeenCalled();
});

test('on without an input chosen: nothing is opened', async () => {
   await render(true, undefined);

   expect(latest).toEqual({ state: 'off' });
   expect(getUserMedia).not.toHaveBeenCalled();
});

test('plays the chosen input to the output, without sending anything, and tells the delay', async () => {
   await render(true, 'v8s');

   expect(getUserMedia.mock.calls[0][0].audio).toMatchObject({
      deviceId: { exact: 'v8s' },
      echoCancellation: false,
      channelCount: 2,
   });
   expect(contexts[0].sources[0].connect).toHaveBeenCalledWith(contexts[0].destination);
   expect(latest).toEqual({ state: 'playing', delayMs: 29 });
});

test('switching it off releases the input and ends the sound', async () => {
   await render(true, 'v8s');

   await render(false, 'v8s');

   expect(trackStop).toHaveBeenCalled();
   expect(contexts[0].close).toHaveBeenCalled();
   expect(latest).toEqual({ state: 'off' });
});

test('a browser that waits for a click says so, and the next click starts the sound', async () => {
   startState = 'suspended';
   resumeAllowed = false;

   await render(true, 'v8s');
   expect(latest).toEqual({ state: 'blocked' });

   resumeAllowed = true;
   await act(async () => {
      document.dispatchEvent(new Event('pointerdown'));
   });

   expect(latest).toMatchObject({ state: 'playing' });
});

test('an input that cannot be opened is an error', async () => {
   getUserMedia.mockRejectedValue(new DOMException('gone', 'NotFoundError'));

   await render(true, 'v8s');

   expect(latest).toEqual({ state: 'error' });
   expect(contexts[0].close).toHaveBeenCalled();
});
