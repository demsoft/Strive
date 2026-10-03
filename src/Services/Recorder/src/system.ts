import { ChildProcess, spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { mkdir, mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { setTimeout as sleep } from 'node:timers/promises';
import { Browser, chromium, Page } from 'playwright-core';
import { Logger } from './logger';

export type RunningProcess = {
   /** resolves with the exit code when the process ends */
   exited: Promise<number | null>;
   /** ask the process to finish by itself (for ffmpeg: "q" on stdin) */
   quit(): void;
   kill(): void;
   readonly pid: number | undefined;
};

export type BrowserHandle = {
   /** resolves when the recording view is ready, rejects if it fails to load */
   waitUntilReady(timeoutMs: number): Promise<void>;
   /** resolves when the page crashed or was closed */
   closed: Promise<void>;
   close(): Promise<void>;
};

export type BrowserOptions = {
   display: string;
   sink: string;
   url: string;
   width: number;
   height: number;
   ignoreHttpsErrors: boolean;
   hostResolverRules?: string;
   /** the port of the remote debugging interface of this browser (every session needs its own) */
   debugPort: number;
   /** where a screenshot of the page is saved if it does not become ready */
   diagnosticsFile?: string;
};

/** Everything the recording session needs from the operating system, so that it can be tested without it. */
export interface SystemOps {
   startXvfb(display: string, width: number, height: number): Promise<RunningProcess>;
   /** Create a virtual sound card the browser plays into, returns a function that removes it. */
   createSink(name: string): Promise<() => Promise<void>>;
   launchBrowser(options: BrowserOptions): Promise<BrowserHandle>;
   spawn(command: string, args: string[]): RunningProcess;
   run(command: string, args: string[]): Promise<string>;
}

/**
 * The page the app window opens before the recording view is loaded. Chromium ignores --app=about:blank and opens a
 * normal window with tabs and an address bar, a real page is needed. It has the colour of the app, so that nothing
 * flashes if the recording starts early.
 */
const APP_START_PAGE = 'data:text/html,<title>Recorder</title><body style="margin:0;background:%230b0d17"></body>';

/** Chromium needs a moment before its debugging port accepts connections. */
async function connectWithRetry(endpoint: string, exited: Promise<void>): Promise<Browser> {
   let stopped = false;
   void exited.then(() => (stopped = true));

   for (let attempt = 0; attempt < 60; attempt++) {
      if (stopped) throw new Error('Chromium exited before it could be controlled');
      try {
         return await chromium.connectOverCDP(endpoint);
      } catch {
         await sleep(500);
      }
   }
   throw new Error('Chromium did not accept a connection');
}

function wrap(child: ChildProcess, log: Logger, name: string): RunningProcess {
   const exited = new Promise<number | null>((resolve) => {
      child.once('exit', (code) => resolve(code));
      child.once('error', (error) => {
         log.error(`${name} could not be started`, { error: String(error) });
         resolve(null);
      });
   });

   child.stderr?.on('data', (data: Buffer) => {
      const text = data.toString().trim();
      if (text) log.warn(`${name}: ${text.slice(0, 500)}`);
   });

   return {
      exited,
      pid: child.pid,
      quit: () => {
         child.stdin?.write('q');
         child.stdin?.end();
      },
      kill: () => {
         if (!child.killed) child.kill('SIGKILL');
      },
   };
}

export function createSystem(log: Logger): SystemOps {
   return {
      async startXvfb(display, width, height) {
         const child = spawn('Xvfb', [display, '-screen', '0', `${width}x${height}x24`, '-nolisten', 'tcp'], {
            stdio: ['pipe', 'ignore', 'pipe'],
         });
         const process = wrap(child, log, 'Xvfb');

         // the X server is ready when its socket exists
         const socket = `/tmp/.X11-unix/X${display.replace(':', '')}`;
         for (let i = 0; i < 50; i++) {
            if (existsSync(socket)) return process;
            await sleep(100);
         }

         process.kill();
         throw new Error(`Xvfb did not start for display ${display}`);
      },

      async createSink(name) {
         const moduleId = (
            await this.run('pactl', ['load-module', 'module-null-sink', `sink_name=${name}`, `sink_properties=device.description=${name}`])
         ).trim();

         return async () => {
            await this.run('pactl', ['unload-module', moduleId]).catch((error) =>
               log.warn('Could not remove the sound sink', { name, error: String(error) }),
            );
         };
      },

      async launchBrowser(options) {
         const args = [
            '--no-sandbox',
            `--window-size=${options.width},${options.height}`,
            '--window-position=0,0',
            '--autoplay-policy=no-user-gesture-required',
            '--disable-infobars',
            '--noerrdialogs',
            '--hide-scrollbars',
            '--force-device-scale-factor=1',
            '--no-first-run',
            '--no-default-browser-check',
            '--disable-features=Translate,MediaRouter',
            '--disable-background-timer-throttling',
            '--disable-renderer-backgrounding',
         ];
         if (options.hostResolverRules) args.push(`--host-resolver-rules=${options.hostResolverRules}`);

         // Start Chromium in app mode ourselves: a window without tab strip and address bar, so that nothing of the
         // browser ends up in the recording. Playwright only attaches over the debugging port (bound to localhost) to
         // open the page and to see when it is ready; the join token travels over that connection, not on a command line.
         const userDataDir = await mkdtemp(join(tmpdir(), 'recorder-chromium-'));
         const child = spawn(
            chromium.executablePath(),
            [
               ...args,
               ...(options.ignoreHttpsErrors ? ['--ignore-certificate-errors'] : []),
               `--remote-debugging-port=${options.debugPort}`,
               `--user-data-dir=${userDataDir}`,
               `--app=${APP_START_PAGE}`,
            ],
            { env: { ...process.env, DISPLAY: options.display, PULSE_SINK: options.sink }, stdio: ['ignore', 'ignore', 'pipe'] },
         );
         child.stderr?.on('data', () => undefined);
         const chromeExited = new Promise<void>((resolve) => child.once('exit', () => resolve()));

         const cleanup = async () => {
            if (child.exitCode === null) {
               child.kill('SIGTERM');
               await Promise.race([chromeExited, sleep(3000)]);
               if (child.exitCode === null) child.kill('SIGKILL');
            }
            await rm(userDataDir, { recursive: true, force: true }).catch(() => undefined);
         };

         let browser: Browser;
         let page: Page;
         try {
            browser = await connectWithRetry(`http://127.0.0.1:${options.debugPort}`, chromeExited);
            const context = browser.contexts()[0];
            page = context.pages()[0] ?? (await context.waitForEvent('page'));
            await page.goto(options.url, { waitUntil: 'domcontentloaded' });
         } catch (error) {
            await cleanup();
            throw error;
         }

         // what the page said, to explain a timeout (kept short, the token in the address is removed)
         const recent: string[] = [];
         const remember = (line: string) => {
            recent.push(line.slice(0, 300));
            if (recent.length > 25) recent.shift();
         };
         page.on('console', (m) => {
            if (m.type() === 'error' || m.type() === 'warning') remember(`console.${m.type()}: ${m.text()}`);
         });
         page.on('pageerror', (e) => remember(`pageerror: ${e.message}`));
         page.on('requestfailed', (r) => remember(`request failed: ${r.url().split('#')[0]} ${r.failure()?.errorText ?? ''}`));
         page.on('response', (r) => {
            if (r.status() >= 400) remember(`http ${r.status()}: ${r.url().split('#')[0]}`);
         });

         const describePage = async () => {
            const url = page.url().split('#')[0];
            const text = await page
               .evaluate(() => document.body?.innerText?.replace(/\s+/g, ' ').slice(0, 300) ?? '')
               .catch(() => '(no page text)');
            if (options.diagnosticsFile) {
               await mkdir(dirname(options.diagnosticsFile), { recursive: true }).catch(() => undefined);
               await page.screenshot({ path: options.diagnosticsFile }).catch(() => undefined);
            }
            return `page ${url}, text "${text}", recent: ${recent.join(' | ') || 'nothing'}`;
         };

         const closed = new Promise<void>((resolve) => {
            page.once('crash', () => resolve());
            page.once('close', () => resolve());
            void chromeExited.then(() => resolve());
         });

         return {
            closed,
            waitUntilReady: async (timeoutMs) => {
               const ready = page.waitForSelector('#recording-ready', { timeout: timeoutMs, state: 'attached' });
               const failed = page
                  .waitForSelector('#recording-no-token, #recording-conference-closed', { timeout: timeoutMs })
                  .then(() => {
                     throw new Error('The recording view could not join the conference');
                  });
               try {
                  await Promise.race([ready, failed]);
               } catch (error) {
                  throw new Error(`${String(error).split('\n')[0]} - ${await describePage()}`);
               }
            },
            close: async () => {
               await browser.close().catch(() => undefined);
               await cleanup();
            },
         };
      },

      spawn(command, args) {
         return wrap(spawn(command, args, { stdio: ['pipe', 'ignore', 'pipe'] }), log, command);
      },

      run(command, args) {
         return new Promise((resolve, reject) => {
            const child = spawn(command, args, { stdio: ['ignore', 'pipe', 'pipe'] });
            let stdout = '';
            let stderr = '';
            child.stdout.on('data', (d: Buffer) => (stdout += d.toString()));
            child.stderr.on('data', (d: Buffer) => (stderr += d.toString()));
            child.once('error', reject);
            child.once('exit', (code) =>
               code === 0 ? resolve(stdout) : reject(new Error(`${command} exited with ${code}: ${stderr.trim()}`)),
            );
         });
      },
   };
}
