import { useEffect, useRef } from 'react';
import appSettings from 'src/config';
import { ClockSample, computeClockOffset } from './sync';

const SAMPLES = 4;
const REFRESH_MS = 5 * 60 * 1000;

async function measure(): Promise<ClockSample[]> {
   const samples: ClockSample[] = [];
   for (let i = 0; i < SAMPLES; i++) {
      const sentAtMs = Date.now();
      try {
         const response = await fetch(`${appSettings.conferenceUrl.replace(/\/+$/, '')}/v1/time`, { cache: 'no-store' });
         if (!response.ok) continue;
         const body = (await response.json()) as { utcMilliseconds: number };
         samples.push({ sentAtMs, receivedAtMs: Date.now(), serverMs: body.utcMilliseconds });
      } catch {
         // the next sample, or the local clock
      }
   }
   return samples;
}

/**
 * The time of the server, to play something together: positions of the video are based on the clock of the server and
 * not on the (often wrong) clock of the computer. Returns a function that gives the current server time in ms.
 */
export default function useServerClock(): () => number {
   const offset = useRef(0);

   useEffect(() => {
      let cancelled = false;
      const update = async () => {
         const samples = await measure();
         if (!cancelled && samples.length > 0) offset.current = computeClockOffset(samples);
      };

      update();
      const timer = setInterval(update, REFRESH_MS);
      return () => {
         cancelled = true;
         clearInterval(timer);
      };
   }, []);

   return () => Date.now() + offset.current;
}
