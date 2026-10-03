import { describe, expect, test } from 'vitest';
import { ConfigError, loadConfig } from '../src/config';

const valid = {
   RECORDER_SHARED_SECRET: '0123456789abcdef',
   STRIVE_API_URL: 'http://strive/',
   WEB_URL: 'https://localhost/',
   STORAGE_BUCKET: 'strive-recordings',
   STORAGE_ACCESS_KEY_ID: 'key',
   STORAGE_SECRET_ACCESS_KEY: 'secret',
};

describe('loadConfig', () => {
   test('reads the settings and applies defaults', () => {
      const config = loadConfig(valid);

      expect(config.port).toBe(3000);
      expect(config.striveApiUrl).toBe('http://strive');
      expect(config.webUrl).toBe('https://localhost');
      expect(config.storage).toMatchObject({ bucket: 'strive-recordings', region: 'auto', forcePathStyle: false });
      expect(config.video).toEqual({ width: 1280, height: 720, fps: 25, crf: 26 });
      expect(config.maxConcurrentRecordings).toBe(2);
      expect(config.ignoreHttpsErrors).toBe(false);
   });

   test('reports every missing variable at once', () => {
      expect(() => loadConfig({})).toThrowError(
         /RECORDER_SHARED_SECRET.*STRIVE_API_URL.*WEB_URL.*STORAGE_BUCKET.*STORAGE_ACCESS_KEY_ID.*STORAGE_SECRET_ACCESS_KEY/,
      );
   });

   test('rejects a short shared secret', () => {
      expect(() => loadConfig({ ...valid, RECORDER_SHARED_SECRET: 'short' })).toThrowError(ConfigError);
   });

   test.each([
      ['VIDEO_FPS', '0'],
      ['VIDEO_FPS', 'abc'],
      ['MAX_CONCURRENT_RECORDINGS', '100'],
      ['PORT', '70000'],
   ])('rejects %s=%s', (name, value) => {
      expect(() => loadConfig({ ...valid, [name]: value })).toThrowError(name);
   });

   test('development switches', () => {
      const config = loadConfig({
         ...valid,
         STORAGE_ENDPOINT: 'http://minio:9000',
         STORAGE_FORCE_PATH_STYLE: 'true',
         IGNORE_HTTPS_ERRORS: 'true',
         HOST_RESOLVER_RULES: 'MAP localhost traefik',
      });

      expect(config.storage.endpoint).toBe('http://minio:9000');
      expect(config.storage.forcePathStyle).toBe(true);
      expect(config.ignoreHttpsErrors).toBe(true);
      expect(config.hostResolverRules).toBe('MAP localhost traefik');
   });
});
