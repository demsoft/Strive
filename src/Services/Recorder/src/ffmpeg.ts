export type RecordArgsOptions = {
   display: string;
   /** the pulseaudio sink the browser plays into, its monitor is recorded */
   sink: string;
   outFile: string;
   width: number;
   height: number;
   fps: number;
   crf: number;
   /** ffmpeg stops by itself after this time, a safety net in addition to the timer of the recorder */
   maxSeconds: number;
};

/**
 * Capture the virtual screen and the audio the browser plays. The output is a fragmented MP4: it is playable even
 * if the process is killed, so a crash does not lose the recording.
 */
export function buildRecordArgs(o: RecordArgsOptions): string[] {
   return [
      '-hide_banner',
      '-loglevel', 'warning',
      '-thread_queue_size', '1024',
      '-f', 'x11grab',
      '-draw_mouse', '0',
      '-framerate', String(o.fps),
      '-video_size', `${o.width}x${o.height}`,
      '-i', `${o.display}.0`,
      // the sound card delivers uneven timestamps: take the clock of the machine, the same one the screen uses
      '-thread_queue_size', '1024',
      '-use_wallclock_as_timestamps', '1',
      '-f', 'pulse',
      '-i', `${o.sink}.monitor`,
      '-t', String(o.maxSeconds),
      '-c:v', 'libx264',
      '-preset', 'veryfast',
      '-crf', String(o.crf),
      '-pix_fmt', 'yuv420p',
      // a keyframe every two seconds: fragments and seeking stay small
      '-g', String(o.fps * 2),
      // keep audio and video in step: fill small gaps and drop small overlaps instead of drifting
      '-af', 'aresample=async=1:first_pts=0',
      '-c:a', 'aac',
      '-b:a', '128k',
      '-ar', '48000',
      '-ac', '2',
      '-movflags', '+frag_keyframe+empty_moov+default_base_moof',
      '-y',
      o.outFile,
   ];
}

/** Rewrite the fragmented file so that browsers know the duration and can seek (no re-encoding). */
export function buildRemuxArgs(input: string, output: string): string[] {
   return ['-hide_banner', '-loglevel', 'error', '-i', input, '-c', 'copy', '-movflags', '+faststart', '-y', output];
}

export function buildProbeArgs(file: string): string[] {
   return ['-v', 'error', '-show_entries', 'format=duration', '-of', 'default=noprint_wrappers=1:nokey=1', file];
}

/** The output of ffprobe is the duration in seconds, or "N/A" if it is unknown. */
export function parseDuration(output: string): number | null {
   const value = Number.parseFloat(output.trim());
   return Number.isFinite(value) && value >= 0 ? value : null;
}
