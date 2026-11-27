import { expect, test } from 'vitest';
import { applyBrandAssets, brandAssets } from './brand-assets';

test('every brand has its own icon and manifest', () => {
   expect(brandAssets('strive')).toEqual({ icon: 'mark-gosmeet.svg', manifest: 'manifest.json' });
   expect(brandAssets('gbedu')).toEqual({ icon: 'mark-gbedu.svg', manifest: 'manifest-gbedu.json' });
   expect(brandAssets('arena')).toEqual({ icon: 'mark-arena.svg', manifest: 'manifest-arena.json' });
});

test('the page is pointed to the files of the brand', () => {
   const page = document.implementation.createHTMLDocument('x');
   const base = page.head.appendChild(page.createElement('base'));
   base.href = 'https://arena.test/';
   const manifest = page.head.appendChild(page.createElement('link'));
   manifest.rel = 'manifest';

   applyBrandAssets('arena', page);

   expect(page.querySelector('link[rel="shortcut icon"]')?.getAttribute('href')).toBe(
      'https://arena.test/mark-arena.svg',
   );
   expect(manifest.getAttribute('href')).toBe('https://arena.test/manifest-arena.json');
});

test('the texts take the name of the brand of the address', async () => {
   const i18n = (await import('./services/i18n')).default;
   await i18n.changeLanguage('en');

   expect(i18n.t('creator.earn_fee')).toBe('GOS MEET commission');
   expect(i18n.t('creator.slug_text')).toContain(`${window.location.host}/u/yourname`);
});
