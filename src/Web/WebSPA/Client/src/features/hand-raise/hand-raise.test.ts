import { events } from 'src/core-hub';
import { onEventOccurred } from 'src/store/signal/actions';
import { RootState } from 'src/store';
import reducer from './reducer';
import { selectIsHandRaised, selectRaisedHandsOrdered } from './selectors';

const stateEvent = onEventOccurred(events.onSynchronizeObjectState);
const updatedEvent = onEventOccurred(events.onSynchronizedObjectUpdated);

const raised = { a: '2024-01-01T10:00:02Z', b: '2024-01-01T10:00:01Z', c: '2024-01-01T10:00:03Z' };

const toRoot = (reduced: ReturnType<typeof reducer>) => ({ handRaise: reduced }) as unknown as RootState;

test('synchronized hand raises, store state', () => {
   const state = reducer(undefined, stateEvent({ id: 'handRaises', value: { raised } } as any));

   expect(state.synchronized).toEqual({ raised });
});

test('other synchronized object, ignore', () => {
   const state = reducer(undefined, stateEvent({ id: 'chat', value: { raised } } as any));

   expect(state.synchronized).toBeNull();
});

test('patch adds and removes hands', () => {
   let state = reducer(undefined, stateEvent({ id: 'handRaises', value: { raised: { a: raised.a } } } as any));

   state = reducer(
      state,
      updatedEvent({
         id: 'handRaises',
         value: [{ op: 'add', path: '/raised/b', value: raised.b }],
      } as any),
   );
   expect(Object.keys(state.synchronized!.raised)).toEqual(['a', 'b']);

   state = reducer(state, updatedEvent({ id: 'handRaises', value: [{ op: 'remove', path: '/raised/a' }] } as any));
   expect(Object.keys(state.synchronized!.raised)).toEqual(['b']);
});

test('selectRaisedHandsOrdered, order by time raised', () => {
   const state = reducer(undefined, stateEvent({ id: 'handRaises', value: { raised } } as any));

   expect(selectRaisedHandsOrdered(toRoot(state))).toEqual(['b', 'a', 'c']);
});

test('selectRaisedHandsOrdered, nothing synchronized yet', () => {
   expect(selectRaisedHandsOrdered(toRoot(reducer(undefined, { type: 'init' })))).toEqual([]);
});

test('selectIsHandRaised', () => {
   const root = toRoot(reducer(undefined, stateEvent({ id: 'handRaises', value: { raised } } as any)));

   expect(selectIsHandRaised(root, 'a')).toBe(true);
   expect(selectIsHandRaised(root, 'x')).toBe(false);
   expect(selectIsHandRaised(root, undefined)).toBe(false);
   expect(selectIsHandRaised(root, null)).toBe(false);
});
