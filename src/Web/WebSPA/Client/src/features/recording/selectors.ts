import { RootState } from 'src/store';

export const selectActiveRecording = (state: RootState) => state.recording.synchronized?.active ?? null;

export const selectRecordingsListOpen = (state: RootState) => state.recording.listOpen;
export const selectRecordingJustFinished = (state: RootState) => state.recording.justFinished;

export const selectIsRecordingAvailable = (state: RootState) =>
   state.conference.conferenceState?.isRecordingEnabled ?? false;
