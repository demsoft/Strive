import Logger from './utils/logger';
import * as mediasoup from 'mediasoup';
import type { WorkerSettings, Worker, WebRtcServer } from 'mediasoup/types';
import { buildWebRtcServerOptions } from './webrtc-server-options';

const logger = new Logger();

export default class MediaSoupWorkers {
   private workers: Worker[] = [];
   private webRtcServers = new Map<Worker, WebRtcServer>();
   private nextWorkerId = 0;

   /**
    * @param webRtcServer when set, every worker gets a WebRtcServer that listens on a single port (see config)
    */
   async run(
      numWorkers: number,
      settings: WorkerSettings,
      webRtcServer?: { basePort: number; listenIp: string; announcedAddress?: string },
   ): Promise<void> {
      logger.info('Initialize Mediasoup %s, run %d mediasoup Workers...', mediasoup.version, numWorkers);

      for (let i = 0; i < numWorkers; ++i) {
         const worker = await mediasoup.createWorker(settings);

         worker.on('died', () => {
            logger.error('mediasoup Worker died, exiting in 2 seconds... [pid:%d]', worker.pid);

            setTimeout(() => process.exit(1), 2000);
         });

         this.workers.push(worker);

         if (webRtcServer) {
            const server = await worker.createWebRtcServer(
               buildWebRtcServerOptions(webRtcServer.basePort, i, webRtcServer.listenIp, webRtcServer.announcedAddress),
            );
            this.webRtcServers.set(worker, server);
            logger.info('WebRtcServer of worker %d listens on port %d', i, webRtcServer.basePort + i);
         }

         // Log worker resource usage every X seconds.
         //   setInterval(async () => {
         //      const usage = await worker.getResourceUsage();

         //      logger.info('mediasoup Worker resource usage [pid:%d]: %o', worker.pid, usage);
         //   }, 120000);
      }

      logger.info('Mediasoup workers started (min_port: %d, max_port: %d)', settings.rtcMinPort, settings.rtcMaxPort);
   }

   getNextWorker(): Worker {
      if (this.workers.length === 0) throw new Error('Please execute run() first');

      const worker = this.workers[this.nextWorkerId];
      if (++this.nextWorkerId === this.workers.length) this.nextWorkerId = 0;

      return worker;
   }

   /** The WebRtcServer of the worker, if workers are run with one. */
   getWebRtcServer(worker: Worker): WebRtcServer | undefined {
      return this.webRtcServers.get(worker);
   }

   close(): void {
      for (const worker of this.workers) {
         worker.close();
      }

      this.workers = [];
      this.webRtcServers.clear();
   }
}
