import { afterEach, expect, test, vi } from 'vitest';

afterEach(() => vi.resetModules());

test('Gbedu and Strive use the key as it is', async () => {
   vi.doMock('./brand-id', () => ({ isArena: false }));
   const { brandKey } = await import('./brand-text');

   expect(brandKey('landing.fans_title')).toBe('landing.fans_title');
});

test('Arena tries its own text first and falls back to the key of Gbedu', async () => {
   vi.doMock('./brand-id', () => ({ isArena: true }));
   const { brandKey } = await import('./brand-text');

   expect(brandKey('landing.fans_title')).toEqual(['arena.landing_fans_title', 'landing.fans_title']);
   expect(brandKey('view_main_gbedu.start_live')).toEqual([
      'arena.view_main_gbedu_start_live',
      'view_main_gbedu.start_live',
   ]);
});
