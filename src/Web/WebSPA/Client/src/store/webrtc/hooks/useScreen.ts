import { ProducerOptions, RtpEncodingParameters } from 'mediasoup-client/lib/types';
import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useDispatch } from 'react-redux';
import { showMessage } from 'src/store/notifier/actions';
import useMedia, { UseMediaState } from './useMedia';
import debug from 'debug';
import { fetchEncodings } from '../utils';

const log = debug('webrtc:hooks:useScreen');

type UseScreenState = UseMediaState & {
   stream: MediaStream | null;
   /** The shared screen comes with sound (a tab, or the system on some browsers) */
   hasAudio: boolean;
};

export const layerResolutions = [1080];
const requestedHeight = 1080;

// Each encoding represents a “spatial layer”. Entries in encodings must be ordered from lowest to highest resolution
// (encodings[0] means “spatial layer 0” while encodings[N-1] means “spatial layer N-1”, being N the number of simulcast streams).
const SCREEN_VIDEO_SIMULCAST_ENCODINGS: RtpEncodingParameters[] = [
   {
      rid: 'h',
   },
];

/**
 * The sound of a shared video or music: the processing of the microphone (echo cancellation, noise suppression,
 * automatic gain) would damage music and make the volume jump, so it is switched off. Stereo, higher bitrate.
 */
const SCREEN_AUDIO_OPTIONS: Partial<ProducerOptions> = {
   codecOptions: { opusStereo: true, opusFec: true, opusDtx: false },
   encodings: [{ maxBitrate: 128000 }],
};

const AUDIO_HINT_SHOWN_KEY = 'strive.screenAudioHintShown';

/**
 * Share the screen: the picture is the producer "screen". If the browser also gives the sound of what is shared, it is
 * sent as a second producer "screen-audio" and stops together with the picture.
 */
export default function useScreen(): UseScreenState {
   const { t } = useTranslation();
   const dispatch = useDispatch();
   const [stream, setStream] = useState<MediaStream | null>(null);
   const audioTrack = useRef<MediaStreamTrack | null>(null);

   const getScreen = async () => {
      log('Request screen with height=%d', requestedHeight);
      const videoConstraints = { height: { ideal: requestedHeight }, frameRate: 30 };
      const mediaDevices = navigator.mediaDevices as any;

      let stream: MediaStream;
      try {
         // Chrome and Edge can share the sound of a tab (or the system on Windows); other browsers ignore the request
         stream = (await mediaDevices.getDisplayMedia({
            video: videoConstraints,
            audio: { echoCancellation: false, noiseSuppression: false, autoGainControl: false },
            systemAudio: 'include',
         })) as MediaStream;
      } catch (error) {
         // the user cancelled the dialog: that is the answer. Anything else: try again without the sound.
         if ((error as DOMException)?.name === 'NotAllowedError' || (error as DOMException)?.name === 'AbortError') {
            throw error;
         }
         log('Screen with audio failed, retry without audio: %O', error);
         stream = (await mediaDevices.getDisplayMedia({ video: videoConstraints })) as MediaStream;
      }
      setStream(stream);

      audioTrack.current = stream.getAudioTracks()[0] ?? null;
      const videoTrack = stream.getVideoTracks()[0];
      // pictures that move (a video that is played) are better with a smooth frame rate than with sharp edges
      if (audioTrack.current) videoTrack.contentHint = 'motion';

      return videoTrack;
   };

   const getOptions: (track: MediaStreamTrack) => Partial<ProducerOptions> = (track) => {
      const settings = track.getSettings();
      log('Got video with %d x %d', settings.width, settings.height);

      const scaledLayers = fetchEncodings(
         settings.height ?? requestedHeight,
         layerResolutions,
         SCREEN_VIDEO_SIMULCAST_ENCODINGS,
      );
      log('Computed layer encodings: %O', scaledLayers);

      return { encodings: scaledLayers };
   };

   const getScreenAudio = async () => {
      const track = audioTrack.current;
      if (!track || track.readyState !== 'live') throw new Error('The shared screen has no sound.');
      return track;
   };

   const video = useMedia('screen', getScreen, getOptions);
   const audio = useMedia('screen-audio', getScreenAudio, SCREEN_AUDIO_OPTIONS);

   // the sound never outlives the picture: the user stopped sharing, the browser ended it or a moderator closed it
   useEffect(() => {
      if (!video.enabled && audio.enabled) audio.disable();
   }, [video.enabled, audio.enabled]);

   const enable = async () => {
      await video.enable();

      if (audioTrack.current && audioTrack.current.readyState === 'live') {
         try {
            await audio.enable();
         } catch (error) {
            // the picture is shared in any case
            log('Could not share the sound of the screen: %O', error);
         }
      } else {
         showAudioHintOnce();
      }
   };

   const showAudioHintOnce = () => {
      try {
         if (sessionStorage.getItem(AUDIO_HINT_SHOWN_KEY)) return;
         sessionStorage.setItem(AUDIO_HINT_SHOWN_KEY, '1');
      } catch {
         // storage is not available: show it every time
      }
      dispatch(showMessage({ type: 'info', message: t('conference.media.screen_no_audio_hint') }));
   };

   const disable = async () => {
      await audio.disable();
      await video.disable();
   };

   const pause = async () => {
      await video.pause();
      await audio.pause();
   };

   const resume = async () => {
      await video.resume();
      await audio.resume();
   };

   return { ...video, enable, disable, pause, resume, stream, hasAudio: audio.enabled };
}
