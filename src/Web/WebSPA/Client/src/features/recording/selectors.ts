import { RootState } from 'src/store';

export const selectActiveRecording = (state: RootState) => state.recording.synchronized?.active ?? null;

export const selectIsRecordingAvailable = (state: RootState) =>
   state.conference.conferenceState?.isRecordingEnabled ?? false;
