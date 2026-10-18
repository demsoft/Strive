/** What a participant measured about their own connection (the server sends it to the others). */
export type LatencyReport = {
   /** round trip time to the media server */
   rttMs: number;
   /** how long incoming sound waits in the buffer of the browser */
   jitterBufferMs: number;
   /** delay of the sound system until the sound is heard */
   outputMs: number;
};

/** Capture, encoding and packing of the sound before it leaves the computer (a packet is 10 to 20 ms). */
export const SEND_PROCESSING_MS = 20;

/**
 * The delay of the sound from one musician to another, in milliseconds, as an estimate from the numbers of both:
 * the way up to the media server (half of the round trip), the way down to the other (half of theirs), the buffer
 * of the other browser and the delay of their sound system.
 */
export function estimateOneWayMs(sender: LatencyReport, receiver: LatencyReport): number {
   return Math.round(
      SEND_PROCESSING_MS + sender.rttMs / 2 + receiver.rttMs / 2 + receiver.jitterBufferMs + receiver.outputMs,
   );
}

export type LatencyVerdict = 'live' | 'slow' | 'turns';

/**
 * What the delay allows. Musicians notice a delay of more than about 30 ms when they play together; slow pieces
 * work to about 60 ms; above that taking turns and playing to the click is the way.
 */
export function verdictFor(ms: number): LatencyVerdict {
   if (ms <= 30) return 'live';
   if (ms <= 60) return 'slow';
   return 'turns';
}

type StatsEntry = Record<string, unknown>;

const entries = (stats: RTCStatsReport): StatsEntry[] => {
   const all: StatsEntry[] = [];
   stats.forEach((entry: StatsEntry) => all.push(entry));
   return all;
};

/** Round trip time of the connection in use (the pair of addresses that was chosen), null if not known. */
export function parseRoundTripMs(stats: RTCStatsReport): number | null {
   const pairs = entries(stats).filter(
      (x) =>
         x.type === 'candidate-pair' &&
         typeof x.currentRoundTripTime === 'number' &&
         (x.nominated || x.state === 'succeeded'),
   );
   if (pairs.length === 0) return null;

   // when several pairs exist the one that is selected has data going through
   const used = pairs.find((x) => (x.bytesSent as number) > 0 || (x.bytesReceived as number) > 0) ?? pairs[0];
   return Math.round((used.currentRoundTripTime as number) * 1000);
}

/** How long incoming audio waits in the buffer on average, null if no audio is received. */
export function parseJitterBufferMs(stats: RTCStatsReport): number | null {
   let delay = 0;
   let emitted = 0;
   for (const x of entries(stats)) {
      if (x.type !== 'inbound-rtp' || (x.kind ?? x.mediaType) !== 'audio') continue;
      if (typeof x.jitterBufferDelay !== 'number' || typeof x.jitterBufferEmittedCount !== 'number') continue;
      delay += x.jitterBufferDelay;
      emitted += x.jitterBufferEmittedCount;
   }

   return emitted > 0 ? Math.round((delay / emitted) * 1000) : null;
}

/** Whether a new measurement differs enough from the last one that was sent to be worth sending. */
export const differsEnough = (a: LatencyReport, b: LatencyReport, thresholdMs = 10) =>
   Math.abs(a.rttMs + a.jitterBufferMs + a.outputMs - (b.rttMs + b.jitterBufferMs + b.outputMs)) >= thresholdMs;
