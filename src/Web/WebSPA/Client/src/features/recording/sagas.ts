import { PayloadAction } from '@reduxjs/toolkit';
import { put, select, takeEvery } from 'redux-saga/effects';
import i18next from 'i18next';
import { events, startRecording, stopRecording } from 'src/core-hub';
import { showErrorOn } from 'src/store/notifier/utils';
import { showMessage } from 'src/store/notifier/actions';
import { connectSignal, onEventOccurred } from 'src/store/signal/actions';
import { RECORDING } from 'src/store/signal/synchronization/synchronized-object-ids';
import { SyncStatePayload } from 'src/core-hub.types';
import { parseSynchronizedObjectId } from 'src/store/signal/synchronization/synchronized-object-id';
import { selectActiveRecording } from './selectors';

/**
 * Recording is never secret: tell everybody when a recording starts or stops, not only the REC indicator.
 * The first state after joining is not announced as news, it only sets the baseline.
 */
let previousRecordingId: string | null | undefined;

export function resetRecordingAnnouncements() {
   previousRecordingId = undefined;
}

export default function* mySaga() {
   yield showErrorOn(startRecording.returnAction);
   yield showErrorOn(stopRecording.returnAction);
   yield takeEvery(connectSignal.type, resetRecordingAnnouncements);
   yield takeEvery(onEventOccurred(events.onSynchronizeObjectState).type, onSync);
   yield takeEvery(onEventOccurred(events.onSynchronizedObjectUpdated).type, onSync);
}

export function* onSync({ payload }: PayloadAction<SyncStatePayload>) {
   if (parseSynchronizedObjectId(payload.id).id !== RECORDING) return;

   const active: ReturnType<typeof selectActiveRecording> = yield select(selectActiveRecording);
   const current = active?.recordingId ?? null;

   if (previousRecordingId === undefined) {
      // joined while a recording is running: nobody may be recorded without knowing it
      if (current) {
         yield put(
            showMessage({ type: 'info', icon: '🔴', message: i18next.t('conference.recording.in_progress_notice') }),
         );
      }
   } else if (previousRecordingId !== current) {
      yield put(
         showMessage({
            type: 'info',
            icon: current ? '🔴' : '⏹️',
            message: i18next.t(current ? 'conference.recording.started_notice' : 'conference.recording.stopped_notice'),
         }),
      );
   }

   previousRecordingId = current;
}
