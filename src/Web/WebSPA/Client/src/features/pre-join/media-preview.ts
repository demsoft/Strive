import { useEffect, useRef, useState } from 'react';
import { AnyInputDevice } from 'src/features/settings/types';

export type PreviewKind = 'audio' | 'video';

/** Constraints that open the selected local device (or the default device if none is selected). */
export function buildConstraints(kind: PreviewKind, device?: AnyInputDevice): MediaStreamConstraints {
   const deviceId = device?.type === 'local' && device.deviceId ? { deviceId: { exact: device.deviceId } } : true;
   return kind === 'audio' ? { audio: deviceId, video: false } : { audio: false, video: deviceId };
}

export type PreviewState = {
   stream: MediaStream | null;
   /** a human readable reason if the device could not be opened */
   error: 'denied' | 'unavailable' | 'unsupported' | null;
};

function toError(error: unknown): PreviewState['error'] {
   const name = (error as { name?: string } | undefined)?.name;
   if (name === 'NotAllowedError' || name === 'SecurityError') return 'denied';
   return 'unavailable';
}

/**
 * Opens a camera or microphone locally (nothing is sent anywhere) while enabled, and stops it again when disabled, the
 * device changes or the component unmounts. Used by the pre-join screen, before the conference connection exists.
 */
export function useMediaPreview(kind: PreviewKind, device: AnyInputDevice | undefined, enabled: boolean): PreviewState {
   const [state, setState] = useState<PreviewState>({ stream: null, error: null });
   const deviceKey = device?.type === 'local' ? device.deviceId : undefined;
   const requestId = useRef(0);

   useEffect(() => {
      if (!enabled) {
         setState({ stream: null, error: null });
         return;
      }

      if (!navigator.mediaDevices?.getUserMedia) {
         setState({ stream: null, error: 'unsupported' });
         return;
      }

      const id = ++requestId.current;
      let opened: MediaStream | null = null;

      navigator.mediaDevices
         .getUserMedia(buildConstraints(kind, device))
         .then((stream) => {
            // a newer request replaced this one while the browser was asking for permission
            if (id !== requestId.current) {
               stream.getTracks().forEach((x) => x.stop());
               return;
            }
            opened = stream;
            setState({ stream, error: null });
         })
         .catch((error) => {
            if (id === requestId.current) setState({ stream: null, error: toError(error) });
         });

      return () => {
         requestId.current++;
         opened?.getTracks().forEach((x) => x.stop());
      };
   }, [kind, deviceKey, enabled]);

   return state;
}

/** The current loudness (0 to 1) of an audio stream, for the microphone level meter. */
export function useAudioLevel(stream: MediaStream | null): number {
   const [level, setLevel] = useState(0);

   useEffect(() => {
      if (!stream || stream.getAudioTracks().length === 0) {
         setLevel(0);
         return;
      }

      const AudioContextType: typeof AudioContext | undefined =
         window.AudioContext ?? (window as any).webkitAudioContext;
      if (!AudioContextType) return;

      const context = new AudioContextType();
      const analyser = context.createAnalyser();
      analyser.fftSize = 512;
      context.createMediaStreamSource(stream).connect(analyser);

      const data = new Uint8Array(analyser.fftSize);
      let frame = 0;
      let last = 0;

      const tick = (time: number) => {
         frame = requestAnimationFrame(tick);
         if (time - last < 80) return; // about 12 updates per second are enough for a meter
         last = time;

         analyser.getByteTimeDomainData(data);
         let sum = 0;
         for (const sample of data) sum += ((sample - 128) / 128) ** 2;
         setLevel(Math.min(1, Math.sqrt(sum / data.length) * 3));
      };
      frame = requestAnimationFrame(tick);

      return () => {
         cancelAnimationFrame(frame);
         context.close();
         setLevel(0);
      };
   }, [stream]);

   return level;
}
