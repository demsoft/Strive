import Axios from 'axios';
import appSettings from 'src/config';

export type { RecordingStatus } from 'src/store/signal/synchronization/synchronized-object-ids';
import { RecordingStatus } from 'src/store/signal/synchronization/synchronized-object-ids';

export type RecordingVisibility = 'signedIn' | 'anyoneWithLink';

export type RecordingDto = {
   recordingId: string;
   status: RecordingStatus;
   startedAt: string;
   endedAt?: string | null;
   durationSeconds?: number | null;
   sizeBytes?: number | null;
   visibility: RecordingVisibility;
   shareToken: string;
   expiresAt: string;
   failureReason?: string | null;
};

export type SharedRecordingDto = {
   conferenceName?: string | null;
   startedAt: string;
   durationSeconds?: number | null;
   url: string;
   urlExpiresAt: string;
};

export async function fetchRecordings(conferenceId: string): Promise<RecordingDto[]> {
   const response = await Axios.get<RecordingDto[]>(
      `${appSettings.conferenceUrl}/v1/conference/${conferenceId}/recordings`,
   );
   return response.data;
}

export async function setVisibility(recordingId: string, visibility: RecordingVisibility): Promise<void> {
   await Axios.patch(`${appSettings.conferenceUrl}/v1/recordings/${recordingId}/visibility`, { visibility });
}

export async function deleteRecording(recordingId: string): Promise<void> {
   await Axios.delete(`${appSettings.conferenceUrl}/v1/recordings/${recordingId}`);
}

/** Works without being signed in if the recording is shared with everyone, otherwise answers 401. */
export async function fetchShared(shareToken: string, accessToken?: string): Promise<SharedRecordingDto> {
   const response = await Axios.get<SharedRecordingDto>(
      `${appSettings.conferenceUrl}/v1/recordings/shared/${encodeURIComponent(shareToken)}`,
      { headers: accessToken ? { Authorization: `Bearer ${accessToken}` } : {} },
   );
   return response.data;
}

export const shareLink = (shareToken: string) => new URL('/r/' + shareToken, document.baseURI).href;
