import { describe, expect, test, vi } from 'vitest';
import { RecorderManager } from '../src/manager';
import { buildServer } from '../src/server';
import { DuplicateRecordingError, RecorderBusyError } from '../src/types';
import { command } from './helpers';

const secret = 'secret-secret-secret';

function create(overrides: Partial<Record<'start' | 'stop' | 'has', unknown>> = {}) {
   const manager = {
      start: vi.fn(),
      stop: vi.fn(async () => true),
      has: vi.fn(() => true),
      activeCount: 1,
      ...overrides,
   };
   return { manager, app: buildServer({ sharedSecret: secret }, manager as unknown as RecorderManager) };
}

const headers = { 'x-recorder-secret': secret };

describe('server', () => {
   test('health needs no secret', async () => {
      const { app } = create();

      const response = await app.inject({ method: 'GET', url: '/health' });

      expect(response.statusCode).toBe(200);
      expect(response.json()).toEqual({ status: 'ok', active: 1 });
   });

   test.each([
      ['no secret', {}],
      ['wrong secret', { 'x-recorder-secret': 'wrong-wrong-wrong-' }],
      ['secret of another length', { 'x-recorder-secret': 'x' }],
   ])('rejects %s', async (_name, requestHeaders) => {
      const { app, manager } = create();

      const response = await app.inject({ method: 'POST', url: '/recordings', headers: requestHeaders, payload: command() });

      expect(response.statusCode).toBe(401);
      expect(manager.start).not.toHaveBeenCalled();
   });

   test('start accepts a command', async () => {
      const { app, manager } = create();

      const response = await app.inject({ method: 'POST', url: '/recordings', headers, payload: command() });

      expect(response.statusCode).toBe(202);
      expect(manager.start).toHaveBeenCalledWith(command());
   });

   test.each([
      [new RecorderBusyError(), 503],
      [new DuplicateRecordingError('rec1'), 409],
      [new RangeError('recordingId is invalid'), 400],
   ])('start maps %s to %d', async (error, status) => {
      const { app } = create({ start: vi.fn(() => { throw error; }) });

      const response = await app.inject({ method: 'POST', url: '/recordings', headers, payload: command() });

      expect(response.statusCode).toBe(status);
   });

   test('stop answers right away and stops in the background', async () => {
      const { app, manager } = create();

      const response = await app.inject({ method: 'POST', url: '/recordings/rec1/stop', headers });

      expect(response.statusCode).toBe(202);
      expect(response.json()).toEqual({ stopping: true });
      expect(manager.stop).toHaveBeenCalledWith('rec1');
   });

   test('stop of an unknown recording is accepted without doing anything', async () => {
      const { app, manager } = create({ has: vi.fn(() => false) });

      const response = await app.inject({ method: 'POST', url: '/recordings/gone/stop', headers });

      expect(response.statusCode).toBe(202);
      expect(response.json()).toEqual({ stopping: false });
      expect(manager.stop).not.toHaveBeenCalled();
   });
});

describe('stats', () => {
   const disk = { freeBytes: 40, totalBytes: 100 };

   test('need the secret', async () => {
      const { app } = create();

      const response = await app.inject({ method: 'GET', url: '/stats' });

      expect(response.statusCode).toBe(401);
   });

   test('tell the load and the disk', async () => {
      const manager = { activeCount: 2 } as unknown as RecorderManager;
      const app = buildServer({ sharedSecret: secret, workDir: '/data', maxConcurrentRecordings: 3 }, manager, async (path) => {
         expect(path).toBe('/data');
         return disk;
      });

      const response = await app.inject({ method: 'GET', url: '/stats', headers });

      expect(response.statusCode).toBe(200);
      expect(response.json()).toEqual({ active: 2, maxConcurrent: 3, disk });
   });

   test('work without disk numbers', async () => {
      const manager = { activeCount: 0 } as unknown as RecorderManager;
      const app = buildServer({ sharedSecret: secret, workDir: '/missing' }, manager, async () => {
         throw new Error('no such directory');
      });

      const response = await app.inject({ method: 'GET', url: '/stats', headers });

      expect(response.json()).toEqual({ active: 0, maxConcurrent: null, disk: null });
   });
});
