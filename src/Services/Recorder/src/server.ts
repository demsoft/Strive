import { timingSafeEqual } from 'node:crypto';
import Fastify, { FastifyInstance } from 'fastify';
import { Config } from './config';
import { RecorderManager } from './manager';
import { DuplicateRecordingError, RecorderBusyError, StartCommand } from './types';

const SECRET_HEADER = 'x-recorder-secret';

function secretsEqual(provided: string | undefined, expected: string): boolean {
   if (!provided) return false;

   const a = Buffer.from(provided);
   const b = Buffer.from(expected);
   return a.length === b.length && timingSafeEqual(a, b);
}

/** The API the Strive server uses to start and stop recordings. Every call needs the shared secret. */
export function buildServer(config: Pick<Config, 'sharedSecret'>, manager: RecorderManager): FastifyInstance {
   const app = Fastify({ logger: false, bodyLimit: 64 * 1024 });

   app.get('/health', async () => ({ status: 'ok', active: manager.activeCount }));

   app.addHook('onRequest', async (request, reply) => {
      if (request.url === '/health') return;

      if (!secretsEqual(request.headers[SECRET_HEADER] as string | undefined, config.sharedSecret)) {
         return reply.code(401).send({ error: 'unauthorized' });
      }
   });

   app.post<{ Body: StartCommand }>('/recordings', async (request, reply) => {
      try {
         manager.start(request.body);
         return reply.code(202).send({ accepted: true });
      } catch (error) {
         if (error instanceof RecorderBusyError) return reply.code(503).send({ error: 'The recorder is busy.' });
         if (error instanceof DuplicateRecordingError) return reply.code(409).send({ error: 'Already recording.' });
         if (error instanceof RangeError) return reply.code(400).send({ error: error.message });
         throw error;
      }
   });

   app.post<{ Params: { id: string } }>('/recordings/:id/stop', async (request, reply) => {
      // stopping takes a while (processing, upload), the result is reported to the server
      const known = manager.has(request.params.id);
      if (known) void manager.stop(request.params.id);
      return reply.code(202).send({ stopping: known });
   });

   return app;
}
