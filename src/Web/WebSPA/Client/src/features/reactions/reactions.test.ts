import { runSaga } from 'redux-saga';
import { events } from 'src/core-hub';
import { ReactionDto } from 'src/core-hub.types';
import { onEventOccurred } from 'src/store/signal/actions';
import { getReactionPosition } from './components/ReactionOverlay';
import { REACTION_EMOJIS } from './emojis';
import reducer, { addReaction, MAX_ACTIVE_REACTIONS, removeReaction } from './reducer';
import { onReaction, REACTION_DISPLAY_MS } from './sagas';

test('addReaction and removeReaction', () => {
   let state = reducer(undefined, addReaction({ id: '1', participantId: 'a', emoji: '👍' }));
   state = reducer(state, addReaction({ id: '2', participantId: 'a', emoji: '👍' }));
   expect(state.active.map((x) => x.id)).toEqual(['1', '2']);

   state = reducer(state, removeReaction('1'));
   expect(state.active.map((x) => x.id)).toEqual(['2']);
});

test('addReaction, drops the oldest above the maximum', () => {
   let state = reducer(undefined, { type: 'init' });
   for (let i = 0; i < MAX_ACTIVE_REACTIONS + 5; i++) {
      state = reducer(state, addReaction({ id: `${i}`, participantId: 'a', emoji: '👍' }));
   }

   expect(state.active).toHaveLength(MAX_ACTIVE_REACTIONS);
   expect(state.active[0].id).toBe('5');
});

describe('onReaction saga', () => {
   beforeEach(() => vi.useFakeTimers());
   afterEach(() => vi.useRealTimers());

   test('shows the reaction and removes it after the display time', async () => {
      const dispatched: any[] = [];
      const action = onEventOccurred<ReactionDto>(events.onReaction)({
         participantId: 'a',
         emoji: '🎉',
         timestamp: '2024-01-01T10:00:00Z',
      });

      const task = runSaga({ dispatch: (x: any) => dispatched.push(x) }, onReaction, action);

      expect(dispatched).toHaveLength(1);
      expect(dispatched[0]).toMatchObject({ type: addReaction.type, payload: { participantId: 'a', emoji: '🎉' } });

      await vi.advanceTimersByTimeAsync(REACTION_DISPLAY_MS);
      await task.toPromise();

      expect(dispatched).toHaveLength(2);
      expect(dispatched[1]).toEqual(removeReaction(dispatched[0].payload.id));
   });
});

test('getReactionPosition, stable and within bounds', () => {
   for (const id of ['a-0', 'a-1', 'some-long-participant-id-42', '']) {
      const position = getReactionPosition(id);
      expect(position).toBe(getReactionPosition(id));
      expect(position).toBeGreaterThanOrEqual(10);
      expect(position).toBeLessThan(90);
   }
});

test('reaction emojis match the server list', () => {
   expect(REACTION_EMOJIS).toEqual(['👍', '👏', '❤️', '😂', '😮', '🎉']);
});
