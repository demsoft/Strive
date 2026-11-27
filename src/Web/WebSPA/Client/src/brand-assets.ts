import { BrandId } from './brand-id';

/** The files in public/ that belong to a brand: its icon and the manifest with its name */
export const brandAssets = (id: BrandId) => ({
   icon: id === 'strive' ? 'mark-gosmeet.svg' : `mark-${id}.svg`,
   manifest: id === 'strive' ? 'manifest.json' : `manifest-${id}.json`,
});

/** Points the page to the icon and the manifest of the brand (the page itself is the same for every address). */
export function applyBrandAssets(id: BrandId, document: Document) {
   const { icon, manifest } = brandAssets(id);
   const link = (rel: string) => document.querySelector<HTMLLinkElement>(`link[rel="${rel}"]`);

   const shortcut = link('shortcut icon') ?? document.head.appendChild(document.createElement('link'));
   shortcut.setAttribute('rel', 'shortcut icon');
   shortcut.setAttribute('type', 'image/svg+xml');
   shortcut.setAttribute('href', new URL(icon, document.baseURI).href);
   link('manifest')?.setAttribute('href', new URL(manifest, document.baseURI).href);
}
