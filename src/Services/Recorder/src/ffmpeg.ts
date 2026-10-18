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

export type AudioRecordArgsOptions = {
   sink: string;
   outFile: string;
   maxSeconds: number;
};

/**
 * Capture only the audio the browser plays (a rehearsal): high quality stereo, a fragmented file like the video.
 */
export function buildAudioRecordArgs(o: AudioRecordArgsOptions): string[] {
   return [
      '-hide_banner',
      '-loglevel', 'warning',
      '-thread_queue_size', '1024',
      '-use_wallclock_as_timestamps', '1',
      '-f', 'pulse',
      '-i', `${o.sink}.monitor`,
      '-t', String(o.maxSeconds),
      '-af', 'aresample=async=1:first_pts=0',
      '-c:a', 'aac',
      '-b:a', '256k',
      '-ar', '48000',
      '-ac', '2',
      '-movflags', '+frag_keyframe+empty_moov+default_base_moof',
      '-y',
      o.outFile,
   ];
}

/** A piece of a track: the file and where it starts on the timeline of the recording (negative: it started earlier). */
export type StemInput = { file: string; offsetMs: number };

/**
 * One track as a lossless file: its pieces are placed at their time (pieces that started before the recording are cut)
 * and the file is padded with silence to the length of the mix, so that all tracks line up when loaded together.
 */
export function buildStemMixArgs(inputs: StemInput[], totalSeconds: number, outFile: string): string[] {
   const args = ['-hide_banner', '-loglevel', 'error'];
   for (const input of inputs) args.push('-i', input.file);

   const parts = inputs.map((input, i) => {
      const filters = ['aresample=48000', 'aformat=channel_layouts=stereo'];
      if (input.offsetMs < 0) {
         filters.push(`atrim=start=${(-input.offsetMs / 1000).toFixed(3)}`, 'asetpts=PTS-STARTPTS');
      } else if (input.offsetMs > 0) {
         filters.push(`adelay=${input.offsetMs}|${input.offsetMs}`);
      }
      return `[${i}:a]${filters.join(',')}[s${i}]`;
   });

   const labels = inputs.map((_, i) => `[s${i}]`).join('');
   const join = inputs.length > 1 ? `${labels}amix=inputs=${inputs.length}:normalize=0:duration=longest,` : labels;
   const graph = `${parts.join(';')};${join}apad=whole_dur=${totalSeconds.toFixed(3)}[out]`;

   args.push('-filter_complex', graph, '-map', '[out]', '-t', totalSeconds.toFixed(3), '-c:a', 'flac', '-y', outFile);
   return args;
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
