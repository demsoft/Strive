import { describe, expect, it } from 'vitest';
import { computeClockOffset, expectedPosition, planSync, PlayerSnapshot } from './sync';

const T0 = Date.parse('2026-01-01T12:00:00Z');
const session = (over: Partial<Parameters<typeof expectedPosition>[0]> = {}) => ({
   state: 'playing' as const,
   positionSeconds: 100,
   rate: 1,
   updatedAt: '2026-01-01T12:00:00Z',
   ...over,
});
const player = (over: Partial<PlayerSnapshot> = {}): PlayerSnapshot => ({
   state: 'playing',
   position: 100,
   rate: 1,
   ...over,
});

describe('expectedPosition', () => {
   it('a playing video moves on with the time', () => {
      expect(expectedPosition(session(), T0 + 5000)).toBe(105);
   });

   it('takes the speed into account', () => {
      expect(expectedPosition(session({ rate: 2 }), T0 + 5000)).toBe(110);
      expect(expectedPosition(session({ rate: 0.5 }), T0 + 10000)).toBe(105);
   });

   it('a paused video stays where it is', () => {
      expect(expectedPosition(session({ state: 'paused' }), T0 + 60000)).toBe(100);
   });

   it('a clock that is a little behind does not move the video backwards', () => {
      expect(expectedPosition(session(), T0 - 3000)).toBe(100);
   });
});

describe('planSync', () => {
   it('does nothing when the player is where it should be', () => {
      expect(planSync(session(), T0 + 5000, player({ position: 105 }))).toEqual([]);
   });

   it('ignores a small difference', () => {
      expect(planSync(session(), T0 + 5000, player({ position: 105.8 }))).toEqual([]);
   });

   it('jumps to the right position when the difference is large', () => {
      expect(planSync(session(), T0 + 5000, player({ position: 90 }))).toEqual([{ type: 'seek', to: 105 }]);
   });

   it('starts a player that is paused while the video plays, after moving it to the right position', () => {
      expect(planSync(session(), T0 + 2000, player({ state: 'paused', position: 50 }))).toEqual([
         { type: 'seek', to: 102 },
         { type: 'play' },
      ]);
   });

   it('starts a player that has not started yet', () => {
      expect(planSync(session(), T0, player({ state: 'idle', position: 100 }))).toEqual([{ type: 'play' }]);
   });

   it('does not start a player that is loading', () => {
      expect(planSync(session(), T0, player({ state: 'buffering', position: 100 }))).toEqual([]);
   });

   it('pauses a player that plays while the video is paused', () => {
      expect(planSync(session({ state: 'paused' }), T0 + 9000, player({ position: 100 }))).toEqual([
         { type: 'pause' },
      ]);
   });

   it('moves a paused player to the position of the pause', () => {
      expect(
         planSync(session({ state: 'paused', positionSeconds: 30 }), T0, player({ state: 'paused', position: 100 })),
      ).toEqual([{ type: 'seek', to: 30 }]);
   });

   it('adjusts the speed', () => {
      expect(planSync(session({ rate: 1.5 }), T0, player({ rate: 1 }))).toEqual([{ type: 'rate', rate: 1.5 }]);
   });
});

describe('computeClockOffset', () => {
   it('is the difference of the server clock to the middle of the round trip', () => {
      // sent at 1000, answered at 1100: the server clock said 5050 in the middle (1050)
      expect(computeClockOffset([{ sentAtMs: 1000, receivedAtMs: 1100, serverMs: 5050 }])).toBe(4000);
   });

   it('trusts the shortest round trip', () => {
      const offset = computeClockOffset([
         { sentAtMs: 0, receivedAtMs: 2000, serverMs: 9000 },
         { sentAtMs: 5000, receivedAtMs: 5040, serverMs: 10020 },
         { sentAtMs: 8000, receivedAtMs: 8500, serverMs: 13000 },
      ]);

      expect(offset).toBe(5000);
   });

   it('is zero without samples', () => {
      expect(computeClockOffset([])).toBe(0);
   });
});
