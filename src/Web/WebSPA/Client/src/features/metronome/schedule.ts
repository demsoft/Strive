/** The click as the server describes it (the server serializes the time as iso text). */
export type MetronomeSession = {
   bpm: number;
   beatsPerBar: number;
   startedBy: string;
   /** server time of the first beat (iso) */
   firstBeatAt: string;
};

export const MIN_BPM = 30;
export const MAX_BPM = 300;
export const MIN_BEATS_PER_BAR = 1;
export const MAX_BEATS_PER_BAR = 12;

export const clampBpm = (bpm: number) => Math.min(MAX_BPM, Math.max(MIN_BPM, Math.round(bpm) || MIN_BPM));

export const beatIntervalMs = (bpm: number) => 60000 / bpm;

/** Server time of beat n (0 is the first beat). */
export const beatTimeMs = (session: MetronomeSession, n: number) =>
   new Date(session.firstBeatAt).getTime() + n * beatIntervalMs(session.bpm);

/** The first beat that is still ahead (or happening exactly now). */
export function nextBeatIndex(session: MetronomeSession, serverNowMs: number): number {
   const elapsed = serverNowMs - new Date(session.firstBeatAt).getTime();
   if (elapsed <= 0) return 0;
   return Math.ceil(elapsed / beatIntervalMs(session.bpm));
}

/** The first beat of a bar gets the accent. */
export const isAccent = (n: number, beatsPerBar: number) => n % beatsPerBar === 0;

/** The beat number inside of the bar, counted from 1 (what musicians count). */
export const beatInBar = (n: number, beatsPerBar: number) => (n % beatsPerBar) + 1;

export type PlannedBeat = { index: number; inMs: number; accent: boolean };

/**
 * The beats to hand to the audio clock now: those from `fromIndex` on that are due within the look-ahead. The audio
 * clock plays them exactly; a timer that checks every few tens of milliseconds is not precise enough for a click.
 */
export function planBeats(
   session: MetronomeSession,
   serverNowMs: number,
   fromIndex: number,
   lookAheadMs: number,
): PlannedBeat[] {
   const planned: PlannedBeat[] = [];
   for (let index = Math.max(fromIndex, nextBeatIndex(session, serverNowMs - 5)); ; index++) {
      const inMs = beatTimeMs(session, index) - serverNowMs;
      if (inMs > lookAheadMs) break;
      planned.push({ index, inMs, accent: isAccent(index, session.beatsPerBar) });
   }
   return planned;
}
