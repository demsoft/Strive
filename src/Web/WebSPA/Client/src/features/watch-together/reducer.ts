import { createSlice } from '@reduxjs/toolkit';
import { connectSignal } from 'src/store/signal/actions';
import { SynchronizedWatchTogether, WATCH_TOGETHER } from 'src/store/signal/synchronization/synchronized-object-ids';
import { synchronizeObjectState } from 'src/store/signal/synchronized-object';

export type WatchTogetherState = {
   synchronized: SynchronizedWatchTogether | null;
};

const initialState: WatchTogetherState = {
   synchronized: null,
};

const watchTogetherSlice = createSlice({
   name: 'watchTogether',
   initialState,
   reducers: {},
   extraReducers: {
      [connectSignal.type]: (state) => {
         state.synchronized = null;
      },
      ...synchronizeObjectState([{ type: 'single', baseId: WATCH_TOGETHER, propertyName: 'synchronized' }]),
   },
});

export default watchTogetherSlice.reducer;
