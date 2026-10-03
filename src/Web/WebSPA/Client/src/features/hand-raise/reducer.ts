import { createSlice } from '@reduxjs/toolkit';
import { HAND_RAISES, SynchronizedHandRaises } from 'src/store/signal/synchronization/synchronized-object-ids';
import { synchronizeObjectState } from 'src/store/signal/synchronized-object';

export type HandRaiseState = {
   synchronized: SynchronizedHandRaises | null;
};

const initialState: HandRaiseState = {
   synchronized: null,
};

const handRaiseSlice = createSlice({
   name: 'handRaise',
   initialState,
   reducers: {},
   extraReducers: {
      ...synchronizeObjectState([{ type: 'single', baseId: HAND_RAISES, propertyName: 'synchronized' }]),
   },
});

export default handRaiseSlice.reducer;
