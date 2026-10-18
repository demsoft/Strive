import { createSlice } from '@reduxjs/toolkit';
import { connectSignal } from 'src/store/signal/actions';
import { METRONOME, SynchronizedMetronome } from 'src/store/signal/synchronization/synchronized-object-ids';
import { synchronizeObjectState } from 'src/store/signal/synchronized-object';

export type MetronomeState = {
   synchronized: SynchronizedMetronome | null;
};

const initialState: MetronomeState = {
   synchronized: null,
};

const metronomeSlice = createSlice({
   name: 'metronome',
   initialState,
   reducers: {},
   extraReducers: {
      [connectSignal.type]: (state) => {
         state.synchronized = null;
      },
      ...synchronizeObjectState([{ type: 'single', baseId: METRONOME, propertyName: 'synchronized' }]),
   },
});

export default metronomeSlice.reducer;
