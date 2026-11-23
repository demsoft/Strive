import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { receiveHrmCallSession } from './handoff';

describe('HRM embedded call handoff', () => {
   const parent = { postMessage: vi.fn() };
   let originalParent: Window;
   let dispose: (() => void) | undefined;
   const onSession = vi.fn();
   const onTimeout = vi.fn();
   const start = (parentOrigin = 'https://hrm.example.test') => {
      dispose = receiveHrmCallSession({ parentOrigin, organizationId: 'org-a', conferenceId: 'call-a', onSession, onTimeout });
   };
   const deliver = (overrides = {}, origin = 'https://hrm.example.test', source: unknown = parent) => {
      const ready = parent.postMessage.mock.calls[0][0];
      window.dispatchEvent(new MessageEvent('message', {
         origin, source: source as Window, data: { ...ready, type: 'goshrm.call.session', token: 'signed-grant', ...overrides },
      }));
   };

   beforeEach(() => {
      vi.useFakeTimers();
      vi.clearAllMocks();
      originalParent = window.parent;
      Object.defineProperty(window, 'parent', { configurable: true, value: parent });
   });
   afterEach(() => {
      dispose?.();
      Object.defineProperty(window, 'parent', { configurable: true, value: originalParent });
      vi.useRealTimers();
   });

   it('announces readiness only to the configured origin and accepts one matching response', () => {
      start();
      expect(parent.postMessage.mock.calls[0][1]).toBe('https://hrm.example.test');
      deliver();
      deliver();
      expect(onSession).toHaveBeenCalledExactlyOnceWith({ organizationId: 'org-a', conferenceId: 'call-a', token: 'signed-grant' });
      vi.runAllTimers();
      expect(onTimeout).not.toHaveBeenCalled();
   });

   it('ignores other origins and windows', () => {
      start();
      deliver({}, 'https://attacker.example.test');
      deliver({}, 'https://hrm.example.test', window);
      expect(onSession).not.toHaveBeenCalled();
   });

   it.each([{ nonce: 'old' }, { organizationId: 'org-b' }, { conferenceId: 'call-b' }, { token: '' }, { token: 'x'.repeat(8193) }])(
      'rejects mismatched or invalid messages: %s', (change) => {
         start();
         deliver(change);
         expect(onSession).not.toHaveBeenCalled();
      },
   );

   it('stops accepting messages after timing out', () => {
      start();
      vi.advanceTimersByTime(15000);
      deliver();
      expect(onTimeout).toHaveBeenCalledOnce();
      expect(onSession).not.toHaveBeenCalled();
   });

   it('cleans up when the call view closes', () => {
      start();
      dispose?.();
      deliver();
      vi.runAllTimers();
      expect(onSession).not.toHaveBeenCalled();
      expect(onTimeout).not.toHaveBeenCalled();
   });

   it.each(['*', 'http://hrm.example.test', 'https://hrm.example.test/path'])('rejects unsafe parent configuration %s', (origin) => {
      expect(() => start(origin)).toThrow();
   });
});
