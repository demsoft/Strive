export type BrandId = 'strive' | 'gbedu' | 'arena';

const STORAGE_KEY = 'strive.brand';

/**
 * One installation serves several brands on their own addresses: gbedu.* is Gbedu (live music sessions), arena.* is
 * Arena (football matches), everything else is Strive. For trying it out without a second address, ?brand=gbedu or
 * ?brand=arena switches the brand of this browser (?brand=strive switches back).
 */
export function detectBrandId(hostname: string, search = '', stored?: string | null): BrandId {
   const requested = new URLSearchParams(search).get('brand');
   if (requested === 'gbedu' || requested === 'arena' || requested === 'strive') return requested;
   const host = hostname.toLowerCase();
   if (host.startsWith('gbedu.')) return 'gbedu';
   if (host.startsWith('arena.')) return 'arena';
   // the choice made with ?brand= holds only on addresses that are not a brand of their own
   return stored === 'gbedu' || stored === 'arena' ? stored : 'strive';
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
export const isArena = brandId === 'arena';
/**
 * Whether this is a creator platform (Gbedu, and Arena which is built on the same creators, sessions, tickets and
 * passes) and not the meetings app. Use isArena for what only Arena does differently.
 */
export const isGbedu = brandId !== 'strive';

/** The name shown to people. */
export const brandName = isArena ? 'Arena' : isGbedu ? 'Gbedu' : 'Strive';
