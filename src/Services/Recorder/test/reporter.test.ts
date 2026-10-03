import { describe, expect, test, vi } from 'vitest';
import { silentLogger } from '../src/logger';
import { HttpReporter } from '../src/reporter';

const config = { striveApiUrl: 'http://strive', sharedSecret: 'secret-secret-secret' };
const ok = () => new Response(null, { status: 200 });

describe('HttpReporter', () => {
   test('posts the report with the shared secret', async () => {
      const fetchFn = vi.fn(async () => ok());

      await new HttpReporter(config, silentLogger, 3, 0, fetchFn as never).report('rec 1', { event: 'finished', sizeBytes: 5 });

      const [url, init] = fetchFn.mock.calls[0] as unknown as [string, RequestInit];
      expect(url).toBe('http://strive/internal/recorder/rec%201/report');
      expect(init.method).toBe('POST');
      expect((init.headers as Record<string, string>)['X-Recorder-Secret']).toBe('secret-secret-secret');
      expect(JSON.parse(init.body as string)).toEqual({ event: 'finished', sizeBytes: 5 });
   });

   test('retries after a server error', async () => {
      const fetchFn = vi
         .fn()
         .mockResolvedValueOnce(new Response(null, { status: 500 }))
         .mockRejectedValueOnce(new Error('connection refused'))
         .mockResolvedValueOnce(ok());

      await new HttpReporter(config, silentLogger, 5, 0, fetchFn as never).report('r', { event: 'started' });

      expect(fetchFn).toHaveBeenCalledTimes(3);
   });

   test('gives up after the maximum number of attempts without throwing', async () => {
      const fetchFn = vi.fn(async () => new Response(null, { status: 503 }));

      await new HttpReporter(config, silentLogger, 3, 0, fetchFn as never).report('r', { event: 'started' });

      expect(fetchFn).toHaveBeenCalledTimes(3);
   });

   test.each([401, 404])('does not retry %d', async (status) => {
      const fetchFn = vi.fn(async () => new Response(null, { status }));

      await new HttpReporter(config, silentLogger, 5, 0, fetchFn as never).report('r', { event: 'started' });

      expect(fetchFn).toHaveBeenCalledTimes(1);
   });
});
