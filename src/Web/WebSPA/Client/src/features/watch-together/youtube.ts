/**
 * The parts of the YouTube IFrame Player API that are used here (https://developers.google.com/youtube/iframe_api_reference).
 * The API is loaded from YouTube when the first video is shown.
 */
export type YouTubePlayerState = -1 | 0 | 1 | 2 | 3 | 5;

export const YT_STATE = { unstarted: -1, ended: 0, playing: 1, paused: 2, buffering: 3, cued: 5 } as const;

export interface YouTubePlayer {
   playVideo(): void;
   pauseVideo(): void;
   seekTo(seconds: number, allowSeekAhead: boolean): void;
   loadVideoById(options: { videoId: string; startSeconds?: number }): void;
   getCurrentTime(): number;
   getPlayerState(): YouTubePlayerState;
   getPlaybackRate(): number;
   setPlaybackRate(rate: number): void;
   setVolume(volume: number): void;
   getVolume(): number;
   mute(): void;
   unMute(): void;
   isMuted(): boolean;
   setSize(width: number, height: number): void;
   getVideoData(): { video_id?: string };
   destroy(): void;
}

export type YouTubePlayerOptions = {
   width: number;
   height: number;
   videoId: string;
   playerVars: Record<string, string | number>;
   events: {
      onReady?: () => void;
      onStateChange?: (event: { data: YouTubePlayerState }) => void;
      onPlaybackRateChange?: (event: { data: number }) => void;
      onError?: (event: { data: number }) => void;
   };
};

export type YouTubeApi = {
   Player: new (element: HTMLElement, options: YouTubePlayerOptions) => YouTubePlayer;
};

declare global {
   interface Window {
      YT?: YouTubeApi;
      onYouTubeIframeAPIReady?: () => void;
   }
}

let apiPromise: Promise<YouTubeApi> | null = null;

export function loadYouTubeApi(): Promise<YouTubeApi> {
   if (window.YT?.Player) return Promise.resolve(window.YT);
   if (apiPromise) return apiPromise;

   apiPromise = new Promise<YouTubeApi>((resolve, reject) => {
      const previous = window.onYouTubeIframeAPIReady;
      window.onYouTubeIframeAPIReady = () => {
         previous?.();
         if (window.YT?.Player) resolve(window.YT);
         else reject(new Error('The YouTube player could not be loaded.'));
      };

      const script = document.createElement('script');
      script.src = 'https://www.youtube.com/iframe_api';
      script.async = true;
      script.onerror = () => {
         apiPromise = null;
         reject(new Error('YouTube could not be reached.'));
      };
      document.head.appendChild(script);
   });

   return apiPromise;
}
