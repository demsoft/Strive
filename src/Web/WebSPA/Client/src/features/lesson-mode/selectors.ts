import { RootState } from 'src/store';

/** only the teachers (moderators) and the participants allowed to speak can be heard */
export const selectLessonModeActive = (state: RootState) => Boolean(state.lessonMode.synchronized?.active);
