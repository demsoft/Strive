import { createSlice } from '@reduxjs/toolkit';
import { connectSignal } from 'src/store/signal/actions';
import { RECORDING, SynchronizedRecording } from 'src/store/signal/synchronization/synchronized-object-ids';
import { synchronizeObjectState } from 'src/store/signal/synchronized-object';

export type RecordingState = {
   synchronized: SynchronizedRecording | null;
};

const initialState: RecordingState = {
   synchronized: null,
};

const recordingSlice = createSlice({
   name: 'recording',
   initialState,
   reducers: {},
   extraReducers: {
      [connectSignal.type]: (state) => {
         state.synchronized = null;
      },
      ...synchronizeObjectState([{ type: 'single', baseId: RECORDING, propertyName: 'synchronized' }]),
   },
});

export default recordingSlice.reducer;
