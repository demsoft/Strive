import { createSlice } from '@reduxjs/toolkit';
import { connectSignal } from 'src/store/signal/actions';
import { LATENCY, SynchronizedLatency } from 'src/store/signal/synchronization/synchronized-object-ids';
import { synchronizeObjectState } from 'src/store/signal/synchronized-object';

export type LatencyState = {
   synchronized: SynchronizedLatency | null;
};

const initialState: LatencyState = {
   synchronized: null,
};

const latencySlice = createSlice({
   name: 'latency',
   initialState,
   reducers: {},
   extraReducers: {
      [connectSignal.type]: (state) => {
         state.synchronized = null;
      },
      ...synchronizeObjectState([{ type: 'single', baseId: LATENCY, propertyName: 'synchronized' }]),
   },
});

export default latencySlice.reducer;
