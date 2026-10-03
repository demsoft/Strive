import { mkdtemp } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { writeFile } from 'node:fs/promises';
import { writeFileSync } from 'node:fs';
import { vi } from 'vitest';
import { Config } from '../src/config';
import { silentLogger } from '../src/logger';
import { BrowserHandle, BrowserOptions, RunningProcess, SystemOps } from '../src/system';
import { Report, Reporter, StartCommand, Uploader } from '../src/types';

export const command = (overrides: Partial<StartCommand> = {}): StartCommand => ({
   recordingId: 'rec1',
   conferenceId: 'conf1',
   joinToken: 'a'.repeat(40),
   storageKey: 'recordings/conf1/rec1.mp4',
   maxDurationMinutes: 60,
   ...overrides,
});

export async function testConfig(overrides: Partial<Config> = {}): Promise<Config> {
   return {
      port: 3000,
      sharedSecret: 'secret-secret-secret',
      striveApiUrl: 'http://strive',
      webUrl: 'https://localhost',
      workDir: await mkdtemp(join(tmpdir(), 'recorder-test-')),
      storage: { bucket: 'b', accessKeyId: 'k', secretAccessKey: 's', region: 'auto', forcePathStyle: false },
      video: { width: 1280, height: 720, fps: 25, crf: 26 },
      maxConcurrentRecordings: 2,
      ignoreHttpsErrors: false,
      ...overrides,
   };
}

export class FakeProcess implements RunningProcess {
   pid = 1;
   quitCalled = false;
   killed = false;
   private resolveExit!: (code: number | null) => void;
   exited = new Promise<number | null>((resolve) => (this.resolveExit = resolve));
   /** finish when asked to quit, like ffmpeg does */
   constructor(private readonly exitOnQuit = true) {}
   quit() {
      this.quitCalled = true;
      if (this.exitOnQuit) this.resolveExit(0);
   }
   kill() {
      this.killed = true;
      this.resolveExit(null);
   }
   exit(code: number | null = 1) {
      this.resolveExit(code);
   }
}

export class FakeSystem implements SystemOps {
   xvfb = new FakeProcess();
   ffmpeg = new FakeProcess();
   sinkRemoved = false;
   browserClosed = false;
   launched: BrowserOptions[] = [];
   runCalls: Array<[string, string[]]> = [];
   failReady = false;
   failRemux = false;
   /** what ffmpeg "recorded": written to the output file when it is started */
   recordedBytes = 50_000;
   private closeResolve!: () => void;
   browserCrashed = new Promise<void>((resolve) => (this.closeResolve = resolve));

   async startXvfb() {
      return this.xvfb;
   }
   async createSink() {
      return async () => {
         this.sinkRemoved = true;
      };
   }
   async launchBrowser(options: BrowserOptions): Promise<BrowserHandle> {
      this.launched.push(options);
      return {
         closed: this.browserCrashed,
         waitUntilReady: async () => {
            if (this.failReady) throw new Error('not ready');
         },
         close: async () => {
            this.browserClosed = true;
         },
      };
   }
   spawn(_command: string, args: string[]) {
      // the capture writes the file ffmpeg was asked to write
      writeFileSync(args[args.length - 1], Buffer.alloc(this.recordedBytes));
      return this.ffmpeg;
   }
   async run(cmd: string, args: string[]) {
      this.runCalls.push([cmd, args]);
      if (cmd === 'ffmpeg') {
         if (this.failRemux) throw new Error('remux failed');
         await writeFile(args[args.length - 1], Buffer.alloc(this.recordedBytes));
         return '';
      }
      if (cmd === 'ffprobe') return '61.25\n';
      return '';
   }
   crashBrowser() {
      this.closeResolve();
   }
}

export class FakeReporter implements Reporter {
   reports: Array<{ id: string; report: Report }> = [];
   async report(id: string, report: Report) {
      this.reports.push({ id, report });
   }
   events() {
      return this.reports.map((x) => x.report.event);
   }
}

export class FakeUploader implements Uploader {
   uploads: Array<{ file: string; key: string }> = [];
   failures = 0;
   upload = vi.fn(async (file: string, key: string) => {
      if (this.failures > 0) {
         this.failures--;
         throw new Error('network down');
      }
      this.uploads.push({ file, key });
      return 123_456;
   });
}
