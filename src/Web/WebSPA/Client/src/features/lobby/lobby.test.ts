import { events } from 'src/core-hub';
import { connectSignal, onEventOccurred } from 'src/store/signal/actions';
import { RootState } from 'src/store';
import reducer from './reducer';
import { selectLobbyStatus, selectWaitingParticipants } from './selectors';

const statusEvent = onEventOccurred(events.onLobbyStatus);
const stateEvent = onEventOccurred(events.onSynchronizeObjectState);
const updatedEvent = onEventOccurred(events.onSynchronizedObjectUpdated);

const participants = {
   a: { displayName: 'Alice', since: '2024-01-01T10:00:02Z' },
   b: { displayName: 'Bob', since: '2024-01-01T10:00:01Z' },
};

const toRoot = (lobby: ReturnType<typeof reducer>) => ({ lobby }) as unknown as RootState;

test('no lobby status initially', () => {
   expect(selectLobbyStatus(toRoot(reducer(undefined, { type: 'init' })))).toBeNull();
});

test('lobby status event stores the status', () => {
   let state = reducer(undefined, statusEvent({ status: 'waiting' }));
   expect(state.status).toBe('waiting');

   state = reducer(state, statusEvent({ status: 'admitted' }));
   expect(state.status).toBe('admitted');
});

test('connecting resets a previous lobby status', () => {
   const state = reducer(
      reducer(undefined, statusEvent({ status: 'denied' })),
      connectSignal('url', {}, [], { conferenceId: 'x' }),
   );

   expect(state.status).toBeNull();
});

test('synchronized lobby is stored and patched', () => {
   let state = reducer(undefined, stateEvent({ id: 'lobby', value: { participants } } as any));
   expect(Object.keys(state.synchronized!.participants)).toEqual(['a', 'b']);

   state = reducer(state, updatedEvent({ id: 'lobby', value: [{ op: 'remove', path: '/participants/a' }] } as any));
   expect(Object.keys(state.synchronized!.participants)).toEqual(['b']);
});

test('selectWaitingParticipants, longest waiting first', () => {
   const state = reducer(undefined, stateEvent({ id: 'lobby', value: { participants } } as any));

   expect(selectWaitingParticipants(toRoot(state)).map((x) => x.id)).toEqual(['b', 'a']);
});

test('selectWaitingParticipants, nothing synchronized', () => {
   expect(selectWaitingParticipants(toRoot(reducer(undefined, { type: 'init' })))).toEqual([]);
});
