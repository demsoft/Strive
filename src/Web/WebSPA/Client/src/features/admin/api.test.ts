import { afterEach, describe, expect, it, vi } from 'vitest';

afterEach(() => vi.resetModules());

async function adminUrlFor(conferenceUrl: string, path: string) {
   vi.doMock('src/config', () => ({ default: { conferenceUrl } }));
   const { adminUrl } = await import('./api');
   return adminUrl(path);
}

describe('adminUrl', () => {
   // the configured address has a trailing slash in some installations and none in others
   it.each([
      ['https://api.example.com', 'https://api.example.com/v1/admin/overview'],
      ['https://api.example.com/', 'https://api.example.com/v1/admin/overview'],
      ['https://api.example.com//', 'https://api.example.com/v1/admin/overview'],
      ['http://localhost:55104', 'http://localhost:55104/v1/admin/overview'],
   ])('%s', async (configured, expected) => {
      expect(await adminUrlFor(configured, 'overview')).toBe(expected);
   });

   it('builds the history address', async () => {
      expect(await adminUrlFor('https://api.example.com/', 'history')).toBe('https://api.example.com/v1/admin/history');
   });
});
