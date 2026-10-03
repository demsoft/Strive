import { vi } from 'vitest';
import { runSaga } from 'redux-saga';
import { events } from 'src/core-hub';
import { RootState } from 'src/store';
import { showMessage } from 'src/store/notifier/actions';
import { connectSignal, onEventOccurred } from 'src/store/signal/actions';
import { formatDuration, formatSize } from './format';
import reducer from './reducer';
import { onSync, resetRecordingAnnouncements } from './sagas';
import { selectActiveRecording, selectIsRecordingAvailable } from './selectors';

vi.mock('i18next', () => ({ default: { t: (key: string) => key } }));

const stateEvent = onEventOccurred(events.onSynchronizeObjectState);
const updatedEvent = onEventOccurred(events.onSynchronizedObjectUpdated);

const active = { recordingId: 'r1', status: 'recording', startedAt: '2024-01-01T10:00:00Z', startedBy: 'mod' };

const toRoot = (recording: ReturnType<typeof reducer>, isRecordingEnabled = true) =>
   ({ recording, conference: { conferenceState: { isRecordingEnabled } } }) as unknown as RootState;

test('formatDuration', () => {
   expect(formatDuration(0)).toBe('0:00');
   expect(formatDuration(65)).toBe('1:05');
   expect(formatDuration(3725)).toBe('1:02:05');
   expect(formatDuration(-5)).toBe('0:00');
   expect(formatDuration(59.9)).toBe('0:59');
});

test('formatSize', () => {
   expect(formatSize(512)).toBe('512 B');
   expect(formatSize(1536)).toBe('1.5 KB');
   expect(formatSize(5 * 1024 * 1024)).toBe('5.0 MB');
   expect(formatSize(3 * 1024 ** 3)).toBe('3.0 GB');
});

test('nothing synchronized, not recording', () => {
   const state = reducer(undefined, { type: 'init' });

   expect(selectActiveRecording(toRoot(state))).toBeNull();
});

test('active recording is synchronized and can end', () => {
   let state = reducer(undefined, stateEvent({ id: 'recording', value: { active } }) as any);
   expect(selectActiveRecording(toRoot(state))?.recordingId).toBe('r1');

   state = reducer(state, updatedEvent({ id: 'recording', value: [{ op: 'replace', path: '/active', value: null }] } as any));
   expect(selectActiveRecording(toRoot(state))).toBeNull();
});

test('connecting resets the recording state', () => {
   let state = reducer(undefined, stateEvent({ id: 'recording', value: { active } }) as any);

   state = reducer(state, connectSignal('url', {}, [], { conferenceId: 'c' }));

   expect(state.synchronized).toBeNull();
});

test('recording availability comes from the conference info', () => {
   const state = reducer(undefined, { type: 'init' });

   expect(selectIsRecordingAvailable(toRoot(state, true))).toBe(true);
   expect(selectIsRecordingAvailable(toRoot(state, false))).toBe(false);
});

describe('announcements', () => {
   async function announce(id: string, current: typeof active | null) {
      const dispatched: any[] = [];
      await runSaga(
         {
            dispatch: (x: any) => dispatched.push(x),
            getState: () => ({ recording: { synchronized: { active: current } } }),
         },
         onSync,
         stateEvent({ id, value: {} }) as any,
      ).toPromise();
      return dispatched.filter((x) => x.type === showMessage.type).map((x) => x.payload.message);
   }

   beforeEach(() => resetRecordingAnnouncements());

   test('joining while a recording runs tells that the meeting is recorded', async () => {
      expect(await announce('recording', active)).toEqual(['conference.recording.in_progress_notice']);
   });

   test('joining without a recording says nothing', async () => {
      expect(await announce('recording', null)).toEqual([]);
   });

   test('a recording that starts and stops is announced, repeated state is not', async () => {
      expect(await announce('recording', null)).toEqual([]);
      expect(await announce('recording', active)).toEqual(['conference.recording.started_notice']);
      expect(await announce('recording', { ...active, status: 'finalizing' })).toEqual([]);
      expect(await announce('recording', null)).toEqual(['conference.recording.stopped_notice']);
   });

   test('other synchronized objects are ignored', async () => {
      expect(await announce('chat', active)).toEqual([]);
   });
});
