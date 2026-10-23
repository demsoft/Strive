import Axios from 'axios';
import appSettings from 'src/config';
import { PublicCreator } from './sessions';

export type VideoStatus = 'uploading' | 'processing' | 'ready' | 'failed';

export type Video = {
   videoId: string;
   title: string;
   description?: string | null;
   status: VideoStatus;
   /** only the creator sees why a video failed */
   failureReason?: string | null;
   durationSeconds?: number | null;
   width?: number | null;
   height?: number | null;
   /** 0: free */
   priceMinor: number;
   createdAt: string;
   isOwner: boolean;
   /** a temporary link to the cover picture */
   posterUrl?: string | null;
   /** a temporary link that plays the video (only when one video is asked for) */
   playbackUrl?: string | null;
   creator?: PublicCreator | null;
};

/** The form the file is sent to the converter with: every field before the file, none changed. */
export type UploadTicket = { url: string; fields: Record<string, string> };

export type CreatedVideo = { video: Video; ticket: UploadTicket };

export { MAX_VIDEO_BYTES, MAX_VIDEO_SECONDS } from 'src/features/videos/limits';

const base = () => `${appSettings.conferenceUrl.replace(/\/+$/, '')}/v1/videos`;
const auth = (accessToken?: string) => (accessToken ? { headers: { Authorization: `Bearer ${accessToken}` } } : {});

export async function createVideo(title: string, description: string, sizeBytes: number): Promise<CreatedVideo> {
   return (await Axios.post<CreatedVideo>(base(), { title, description: description || undefined, sizeBytes })).data;
}

/** Says the file arrived at the converter: the server checks it and conversion starts. */
export async function completeUpload(videoId: string): Promise<Video> {
   return (await Axios.post<Video>(`${base()}/${encodeURIComponent(videoId)}/uploaded`)).data;
}

export async function fetchMyVideos(): Promise<Video[]> {
   return (await Axios.get<Video[]>(`${base()}/mine`)).data;
}

export async function fetchVideo(videoId: string, accessToken?: string): Promise<Video> {
   return (await Axios.get<Video>(`${base()}/${encodeURIComponent(videoId)}`, auth(accessToken))).data;
}

export async function deleteVideo(videoId: string): Promise<void> {
   await Axios.delete(`${base()}/${encodeURIComponent(videoId)}`);
}

export class UploadError extends Error {}

/**
 * Sends the file straight to the converter (not through our server) and reports how far it is. The answer of the
 * converter is only read for its error message: what happens next is the business of our server.
 */
export function uploadToConverter(
   ticket: UploadTicket,
   file: File,
   onProgress: (fraction: number) => void,
   makeRequest: () => XMLHttpRequest = () => new XMLHttpRequest(),
): { promise: Promise<void>; abort: () => void } {
   const request = makeRequest();
   const promise = new Promise<void>((resolve, reject) => {
      const form = new FormData();
      Object.entries(ticket.fields).forEach(([key, value]) => form.append(key, value));
      form.append('file', file);

      request.upload.onprogress = (event) => {
         if (event.lengthComputable && event.total > 0) onProgress(Math.min(1, event.loaded / event.total));
      };
      request.onload = () => {
         if (request.status >= 200 && request.status < 300) return resolve();

         // the converter says what is wrong: "File size too large", "Invalid image file", ...
         let message = '';
         try {
            message = JSON.parse(request.responseText)?.error?.message ?? '';
         } catch {
            // not json
         }
         reject(new UploadError(message));
      };
      request.onerror = () => reject(new UploadError(''));
      request.onabort = () => reject(new UploadError('aborted'));

      request.open('POST', ticket.url);
      request.send(form);
   });

   return { promise, abort: () => request.abort() };
}
