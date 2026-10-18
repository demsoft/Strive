import { useEffect, useRef, useState } from 'react';
import { MetronomeSession, planBeats } from './schedule';

const LOOK_AHEAD_MS = 250;
const TICK_MS = 50;

/** A short wooden click. The accent is higher. */
function playClick(context: AudioContext, when: number, accent: boolean, volume: number) {
   const oscillator = context.createOscillator();
   const gain = context.createGain();
   oscillator.frequency.value = accent ? 1600 : 1000;
   gain.gain.setValueAtTime(0.0001, when);
   gain.gain.exponentialRampToValueAtTime(Math.max(0.0002, volume), when + 0.002);
   gain.gain.exponentialRampToValueAtTime(0.0001, when + 0.045);
   oscillator.connect(gain);
   gain.connect(context.destination);
   oscillator.start(when);
   oscillator.stop(when + 0.06);
}

/**
 * Plays the click of the metronome in time with the clock of the server, the same moment for everybody. The beats
 * are handed to the clock of the audio system a little ahead, so they sound exactly on time. Returns the number of the
 * beat that was heard last (for the display), or null before the first beat.
 *
 * @param getServerNow the time of the server in milliseconds (see useServerClock)
 */
export default function useMetronomeClick(
   session: MetronomeSession | null,
   getServerNow: () => number,
   volume: number,
   muted: boolean,
): number | null {
   const [beat, setBeat] = useState<number | null>(null);
   const contextRef = useRef<AudioContext | null>(null);
   const volumeRef = useRef(volume);
   const mutedRef = useRef(muted);
   volumeRef.current = volume;
   mutedRef.current = muted;

   useEffect(() => {
      setBeat(null);
      if (!session) return;

      let context = contextRef.current;
      if (!context) {
         context = new AudioContext({ latencyHint: 'interactive' });
         contextRef.current = context;
      }
      context.resume().catch(() => undefined);

      let scheduledUpTo = -1;
      const timeouts = new Set<ReturnType<typeof setTimeout>>();

      const tick = () => {
         if (!context || context.state !== 'running') {
            context?.resume().catch(() => undefined);
            return;
         }

         const serverNow = getServerNow();
         // the sound needs a moment to get from the program to the ear: start it that much earlier
         const outputLatencyMs = ((context.outputLatency || 0) + (context.baseLatency || 0)) * 1000;

         for (const planned of planBeats(session, serverNow, scheduledUpTo + 1, LOOK_AHEAD_MS)) {
            scheduledUpTo = planned.index;
            const delayMs = planned.inMs - outputLatencyMs;
            if (delayMs < -20) continue; // too late, skipping is better than a click out of time

            if (!mutedRef.current) {
               playClick(context, context.currentTime + Math.max(0, delayMs) / 1000, planned.accent, volumeRef.current);
            }
            const timeout = setTimeout(() => {
               timeouts.delete(timeout);
               setBeat(planned.index);
            }, Math.max(0, planned.inMs));
            timeouts.add(timeout);
         }
      };

      tick();
      const timer = setInterval(tick, TICK_MS);
      return () => {
         clearInterval(timer);
         timeouts.forEach(clearTimeout);
      };
   }, [session?.bpm, session?.beatsPerBar, session?.firstBeatAt]);

   // the audio context is released when the meeting is left
   useEffect(
      () => () => {
         contextRef.current?.close().catch(() => undefined);
         contextRef.current = null;
      },
      [],
   );

   return beat;
}
