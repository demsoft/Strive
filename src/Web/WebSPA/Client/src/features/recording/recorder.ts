/** Recorders join conferences as participants with this id prefix (see RecorderParticipants on the server). */
export const RECORDER_PARTICIPANT_PREFIX = 'recorder-';

export const isRecorderParticipant = (participantId: string) => participantId.startsWith(RECORDER_PARTICIPANT_PREFIX);

export type RecorderToken = { token: string; participantId: string };

/**
 * The recording view is opened by the recorder service as /c/{conferenceId}/recording#token={jwt}. The token is in the
 * fragment so that it is never sent to a server. The participant id is the subject of the token.
 */
export function parseRecorderToken(hash: string): RecorderToken | null {
   const token = new URLSearchParams(hash.replace(/^#/, '')).get('token');
   if (!token) return null;

   try {
      const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
      const participantId = payload.sub as string | undefined;
      if (!participantId) return null;

      return { token, participantId };
   } catch {
      return null;
   }
}
