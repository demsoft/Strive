import { RootState } from 'src/store';

/** the video that everybody watches together, null if there is none */
export const selectWatchTogetherSession = (state: RootState) => state.watchTogether.synchronized?.session ?? null;
