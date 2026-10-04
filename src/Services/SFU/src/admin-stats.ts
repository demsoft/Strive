import * as fs from 'fs';
import * as os from 'os';
import ConferenceManager from './lib/conference/conference-manager';
import MediaSoupWorkers, { WorkerLoad } from './media-soup-workers';

export type SfuStats = {
   uptimeSeconds: number;
   workers: WorkerLoad[];
   conferences: { conferenceId: string; participants: number; connections: number; producers: number; consumers: number }[];
   totals: { conferences: number; participants: number; connections: number; producers: number; consumers: number };
   system: {
      /** the machine (or the Docker environment) the media server runs in */
      cpuCores: number;
      loadAverage1m: number;
      memoryTotalBytes: number;
      /** memory that programs can still get (free plus what the system can give back, like caches) */
      memoryAvailableBytes: number;
      processMemoryBytes: number;
   };
};

/** MemAvailable of /proc/meminfo in bytes, or undefined if the text has none (not Linux). */
export function parseMemAvailable(meminfo: string): number | undefined {
   const match = /^MemAvailable:\s+(\d+)\s+kB/m.exec(meminfo);
   return match ? Number(match[1]) * 1024 : undefined;
}

function readAvailableMemory(): number {
   try {
      const available = parseMemAvailable(fs.readFileSync('/proc/meminfo', 'utf8'));
      if (available !== undefined) return available;
   } catch {
      // not Linux
   }
   return os.freemem();
}

/** What the admin overview of the API shows about the media server. */
export async function collectSfuStats(workers: MediaSoupWorkers, conferences: ConferenceManager): Promise<SfuStats> {
   const conferenceStats = conferences.getConferenceStats();
   const sum = (pick: (x: (typeof conferenceStats)[number]) => number) => conferenceStats.reduce((a, b) => a + pick(b), 0);

   return {
      uptimeSeconds: Math.round(process.uptime()),
      workers: await workers.getLoad(),
      conferences: conferenceStats,
      totals: {
         conferences: conferenceStats.length,
         participants: sum((x) => x.participants),
         connections: sum((x) => x.connections),
         producers: sum((x) => x.producers),
         consumers: sum((x) => x.consumers),
      },
      system: {
         cpuCores: os.cpus().length,
         loadAverage1m: os.loadavg()[0],
         memoryTotalBytes: os.totalmem(),
         memoryAvailableBytes: readAvailableMemory(),
         processMemoryBytes: process.memoryUsage().rss,
      },
   };
}
