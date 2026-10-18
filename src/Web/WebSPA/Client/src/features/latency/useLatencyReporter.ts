import { useEffect, useRef, useState } from 'react';
import { useDispatch } from 'react-redux';
import * as coreHub from 'src/core-hub';
import useWebRtc from 'src/store/webrtc/hooks/useWebRtc';
import { differsEnough, LatencyReport, parseJitterBufferMs, parseRoundTripMs } from './estimate';

const MEASURE_EVERY_MS = 4000;
/** the server learns of a new number at most this often, unless it changed a lot (see differsEnough) */
const SEND_AT_LEAST_EVERY_MS = 30000;

/**
 * Measures how good the own connection is and tells the others (musicians use it to see which of them can play
 * together). Only runs while it is wanted; returns the last measurement for the display.
 */
export default function useLatencyReporter(enabled: boolean): LatencyReport | null {
   const connection = useWebRtc();
   const dispatch = useDispatch();
   const [report, setReport] = useState<LatencyReport | null>(null);
   const audioContext = useRef<AudioContext | null>(null);
   const lastSent = useRef<{ report: LatencyReport; at: number } | null>(null);
   const lastBuffer = useRef(0);

   useEffect(() => {
      if (!enabled || !connection) return;
      let cancelled = false;

      const measure = async () => {
         const sendStats = await connection.sendTransport?.getStats().catch(() => undefined);
         const receiveStats = await connection.receiveTransport?.getStats().catch(() => undefined);
         if (cancelled || !sendStats) return;

         const rttMs = parseRoundTripMs(sendStats) ?? (receiveStats ? parseRoundTripMs(receiveStats) : null);
         if (rttMs === null) return;

         // without incoming audio there is nothing to measure: keep the last value
         const buffer = receiveStats ? parseJitterBufferMs(receiveStats) : null;
         if (buffer !== null) lastBuffer.current = buffer;

         audioContext.current ??= new AudioContext({ latencyHint: 'interactive' });
         const outputMs = Math.round(
            ((audioContext.current.baseLatency || 0) + (audioContext.current.outputLatency || 0)) * 1000,
         );

         const next: LatencyReport = { rttMs, jitterBufferMs: lastBuffer.current, outputMs };
         setReport(next);

         const sent = lastSent.current;
         if (!sent || differsEnough(sent.report, next) || Date.now() - sent.at > SEND_AT_LEAST_EVERY_MS) {
            lastSent.current = { report: next, at: Date.now() };
            dispatch(coreHub.reportLatency(next));
         }
      };

      measure();
      const timer = setInterval(measure, MEASURE_EVERY_MS);
      return () => {
         cancelled = true;
         clearInterval(timer);
      };
   }, [enabled, connection]);

   useEffect(
      () => () => {
         audioContext.current?.close().catch(() => undefined);
         audioContext.current = null;
      },
      [],
   );

   return report;
}
