import { mkdir } from 'node:fs/promises';
import { ConfigError, loadConfig } from './config';
import { createLogger } from './logger';
import { RecorderManager } from './manager';
import { HttpReporter } from './reporter';
import { buildServer } from './server';
import { createSystem } from './system';
import { S3Uploader } from './uploader';

async function main() {
   const log = createLogger({ service: 'recorder' });

   let config;
   try {
      config = loadConfig();
   } catch (error) {
      if (error instanceof ConfigError) {
         log.error(error.message);
         process.exit(1);
      }
      throw error;
   }

   await mkdir(config.workDir, { recursive: true });

   const manager = new RecorderManager(
      {
         config,
         system: createSystem(log),
         reporter: new HttpReporter(config, log),
         uploader: new S3Uploader(config.storage, log),
         log,
      },
      log,
   );

   const recovered = await manager.recover();
   if (recovered > 0) log.info('Recovered recordings', { count: recovered });

   const app = buildServer(config, manager);
   await app.listen({ port: config.port, host: '0.0.0.0' });
   log.info('Recorder listening', { port: config.port, maxConcurrentRecordings: config.maxConcurrentRecordings });

   const shutdown = async (signal: string) => {
      log.info('Shutting down', { signal });
      await app.close();
      await manager.shutdown();
      process.exit(0);
   };
   process.on('SIGTERM', () => void shutdown('SIGTERM'));
   process.on('SIGINT', () => void shutdown('SIGINT'));
}

main().catch((error) => {
   console.error(error);
   process.exit(1);
});
