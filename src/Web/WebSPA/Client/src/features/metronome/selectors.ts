import { RootState } from 'src/store';

/** the click that is running, null if there is none */
export const selectMetronomeSession = (state: RootState) => state.metronome.synchronized?.session ?? null;
