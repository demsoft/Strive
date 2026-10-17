import { createSlice } from '@reduxjs/toolkit';
import { connectSignal } from 'src/store/signal/actions';
import { LESSON_MODE, SynchronizedLessonMode } from 'src/store/signal/synchronization/synchronized-object-ids';
import { synchronizeObjectState } from 'src/store/signal/synchronized-object';

export type LessonModeState = {
   synchronized: SynchronizedLessonMode | null;
};

const initialState: LessonModeState = {
   synchronized: null,
};

const lessonModeSlice = createSlice({
   name: 'lessonMode',
   initialState,
   reducers: {},
   extraReducers: {
      [connectSignal.type]: (state) => {
         state.synchronized = null;
      },
      ...synchronizeObjectState([{ type: 'single', baseId: LESSON_MODE, propertyName: 'synchronized' }]),
   },
});

export default lessonModeSlice.reducer;
