import { createSlice, PayloadAction } from '@reduxjs/toolkit';
import { connectSignal } from 'src/store/signal/actions';
import { RECORDING, SynchronizedRecording } from 'src/store/signal/synchronization/synchronized-object-ids';
import { synchronizeObjectState } from 'src/store/signal/synchronized-object';

export type RecordingState = {
   synchronized: SynchronizedRecording | null;
   /** the window with the recordings of the conference is open */
   listOpen: boolean;
   /** it was opened because a recording has just been finished: the link is shown first */
   justFinished: boolean;
};

const initialState: RecordingState = {
   synchronized: null,
   listOpen: false,
   justFinished: false,
};

const recordingSlice = createSlice({
   name: 'recording',
   initialState,
   reducers: {
      openRecordings(state, action: PayloadAction<{ justFinished?: boolean } | undefined>) {
         state.listOpen = true;
         state.justFinished = Boolean(action.payload?.justFinished);
      },
      closeRecordings(state) {
         state.listOpen = false;
         state.justFinished = false;
      },
   },
   extraReducers: {
      [connectSignal.type]: (state) => {
         state.synchronized = null;
      },
      ...synchronizeObjectState([{ type: 'single', baseId: RECORDING, propertyName: 'synchronized' }]),
   },
});

export const { openRecordings, closeRecordings } = recordingSlice.actions;
export default recordingSlice.reducer;
