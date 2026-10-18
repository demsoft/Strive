import { useEffect, useState } from 'react';
import { MUSIC_AUDIO_CONSTRAINTS } from './useMicrophone';

export type InstrumentMonitorStatus =
   /** switched off, or no input chosen */
   | { state: 'off' }
   | { state: 'starting' }
   /** the browser waits for a click on the page before it plays sound */
   | { state: 'blocked' }
   | { state: 'playing'; delayMs: number }
   /** the input cannot be opened (taken away, unplugged) */
   | { state: 'error' };

/**
 * Plays the chosen sound card input to the musician in the headphones, straight from the capture in the browser: not
 * through the server, so without the delay of the call, and without sending anything (it works before the instrument is
 * turned on for the others, which is also how the volume is adjusted). Only with headphones: with speakers it feeds back.
 */
export default function useInstrumentMonitor(enabled: boolean, deviceId: string | undefined): InstrumentMonitorStatus {
   const [status, setStatus] = useState<InstrumentMonitorStatus>({ state: 'off' });

   useEffect(() => {
      if (!enabled || !deviceId) {
         setStatus({ state: 'off' });
         return;
      }

      let cancelled = false;
      let stream: MediaStream | undefined;
      let context: AudioContext | undefined;
      let removeUnlock: (() => void) | undefined;
      setStatus({ state: 'starting' });

      const delayOf = (c: AudioContext) => Math.round(((c.baseLatency || 0) + (c.outputLatency || 0)) * 1000);

      (async () => {
         try {
            context = new AudioContext({ latencyHint: 'interactive' });
            const current = context;
            current.resume().catch(() => undefined);

            stream = await navigator.mediaDevices.getUserMedia({
               audio: { ...MUSIC_AUDIO_CONSTRAINTS, deviceId: { exact: deviceId } },
            });
            if (cancelled) return;

            current.createMediaStreamSource(stream).connect(current.destination);
            stream.getAudioTracks()[0]?.addEventListener('ended', () => !cancelled && setStatus({ state: 'error' }));

            const update = () => {
               if (cancelled) return;
               setStatus(
                  current.state === 'running' ? { state: 'playing', delayMs: delayOf(current) } : { state: 'blocked' },
               );
            };
            current.addEventListener('statechange', update);

            if (current.state !== 'running') {
               // the browser only plays sound after a click on the page: the next one starts it
               const unlock = () => current.resume().catch(() => undefined);
               document.addEventListener('pointerdown', unlock, { once: true });
               document.addEventListener('keydown', unlock, { once: true });
               removeUnlock = () => {
                  document.removeEventListener('pointerdown', unlock);
                  document.removeEventListener('keydown', unlock);
               };
            }
            update();
         } catch {
            context?.close().catch(() => undefined);
            if (!cancelled) setStatus({ state: 'error' });
         }
      })();

      return () => {
         cancelled = true;
         removeUnlock?.();
         stream?.getTracks().forEach((track) => track.stop());
         context?.close().catch(() => undefined);
      };
   }, [enabled, deviceId]);

   return status;
}
