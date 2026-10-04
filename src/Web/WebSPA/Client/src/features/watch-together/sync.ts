import { WatchTogetherSession } from 'src/store/signal/synchronization/synchronized-object-ids';

type PlaybackSession = Pick<WatchTogetherSession, 'state' | 'positionSeconds' | 'rate' | 'updatedAt'>;

/** Where the video should be at the given (server) time. */
export function expectedPosition(session: PlaybackSession, serverNowMs: number): number {
   if (session.state === 'paused') return session.positionSeconds;

   const elapsedSeconds = Math.max(0, (serverNowMs - Date.parse(session.updatedAt)) / 1000);
   return session.positionSeconds + elapsedSeconds * session.rate;
}

/** What the local player does right now. 'buffering' also covers a video that has not started yet. */
export type PlayerSnapshot = {
   state: 'playing' | 'paused' | 'buffering' | 'idle';
   position: number;
   rate: number;
};

export type SyncCommand =
   | { type: 'play' }
   | { type: 'pause' }
   | { type: 'seek'; to: number }
   | { type: 'rate'; rate: number };

/** A difference below this is not corrected: seeking all the time would be worse than being a little off. */
export const PLAYING_TOLERANCE_SECONDS = 1.2;
export const PAUSED_TOLERANCE_SECONDS = 0.5;

/**
 * What the local player has to do to follow the shared playback. Used on a regular basis by everybody, including the
 * participants that control the video.
 */
export function planSync(
   session: PlaybackSession,
   serverNowMs: number,
   player: PlayerSnapshot,
   playingTolerance = PLAYING_TOLERANCE_SECONDS,
): SyncCommand[] {
   const commands: SyncCommand[] = [];
   const expected = expectedPosition(session, serverNowMs);

   if (player.rate !== session.rate) commands.push({ type: 'rate', rate: session.rate });

   if (session.state === 'playing') {
      if (Math.abs(player.position - expected) > playingTolerance) commands.push({ type: 'seek', to: expected });
      if (player.state === 'paused' || player.state === 'idle') commands.push({ type: 'play' });
   } else {
      if (player.state === 'playing' || player.state === 'buffering') commands.push({ type: 'pause' });
      if (Math.abs(player.position - expected) > PAUSED_TOLERANCE_SECONDS)
         commands.push({ type: 'seek', to: expected });
   }

   return commands;
}

export type ClockSample = { sentAtMs: number; receivedAtMs: number; serverMs: number };

/**
 * The difference between the clock of the server and the local clock (add it to the local time to get the server
 * time). The sample with the shortest round trip is the most exact: the server answered somewhere in the middle.
 */
export function computeClockOffset(samples: ClockSample[]): number {
   if (samples.length === 0) return 0;

   const best = samples.reduce((a, b) => (b.receivedAtMs - b.sentAtMs < a.receivedAtMs - a.sentAtMs ? b : a));
   const middle = best.sentAtMs + (best.receivedAtMs - best.sentAtMs) / 2;
   return best.serverMs - middle;
}
