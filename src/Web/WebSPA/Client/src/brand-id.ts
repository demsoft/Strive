export type BrandId = 'strive' | 'gbedu';

const STORAGE_KEY = 'strive.brand';

/**
 * One installation serves several brands on their own addresses: gbedu.* is Gbedu (live music sessions), everything
 * else is Strive. For trying it out without a second address, ?brand=gbedu switches the brand of this browser
 * (?brand=strive switches back).
 */
export function detectBrandId(hostname: string, search = '', stored?: string | null): BrandId {
   const requested = new URLSearchParams(search).get('brand');
   if (requested === 'gbedu' || requested === 'strive') return requested;
   if (hostname.toLowerCase().startsWith('gbedu.')) return 'gbedu';
   // the choice made with ?brand= holds only on addresses that are not a brand of their own
   return stored === 'gbedu' ? 'gbedu' : 'strive';
}

function read(): BrandId {
   let stored: string | null = null;
   try {
      stored = window.sessionStorage.getItem(STORAGE_KEY);
   } catch {
      // storage can be blocked
   }

   const id = detectBrandId(window.location.hostname, window.location.search, stored);
   try {
      if (new URLSearchParams(window.location.search).get('brand')) window.sessionStorage.setItem(STORAGE_KEY, id);
   } catch {
      // storage can be blocked
   }
   return id;
}

export const brandId: BrandId = read();
export const isGbedu = brandId === 'gbedu';

/** The name shown to people. */
export const brandName = isGbedu ? 'Gbedu' : 'Strive';
