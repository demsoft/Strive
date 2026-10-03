import type { WebRtcServerOptions } from 'mediasoup/types';

/**
 * The listen info of the WebRtcServer of the worker with the given index: one udp and one tcp port per worker, the
 * port is the base port plus the index.
 */
export function buildWebRtcServerOptions(
   basePort: number,
   workerIndex: number,
   listenIp: string,
   announcedAddress?: string,
): WebRtcServerOptions {
   const port = basePort + workerIndex;
   return {
      listenInfos: [
         { protocol: 'udp', ip: listenIp, announcedAddress: announcedAddress || undefined, port },
         { protocol: 'tcp', ip: listenIp, announcedAddress: announcedAddress || undefined, port },
      ],
   };
}
