/** How long a pass lasts (the server decides, this is what is shown). */
export const PASS_DAYS = 30;

/** 500000 kobo is ₦5,000. Whole amounts are shown without decimals. */
export function formatMoney(minor: number, currency: string, locale = 'en-NG'): string {
   const major = minor / 100;
   return new Intl.NumberFormat(locale, {
      style: 'currency',
      currency,
      minimumFractionDigits: Number.isInteger(major) ? 0 : 2,
      maximumFractionDigits: 2,
   }).format(major);
}

/**
 * What a person types as a price ("5000", "5,000", "₦5 000.50") in minor units, or null if it is not an amount.
 * Whole minor units only: the server never sees fractions of a kobo.
 */
export function parsePriceToMinor(input: string): number | null {
   const cleaned = input.replace(/[^\d.]/g, '');
   if (!/^\d+(\.\d{1,2})?$/.test(cleaned)) return null;

   return Math.round(Number(cleaned) * 100);
}

export const MIN_PRICE_MINOR = 10_000;
export const MAX_PRICE_MINOR = 100_000_000;
export const isPriceInRange = (minor: number) => minor >= MIN_PRICE_MINOR && minor <= MAX_PRICE_MINOR;

/** What a creator can choose as the length of a session, in minutes (the server accepts 15 to 480). */
export const SESSION_DURATIONS = [30, 45, 60, 90, 120, 180] as const;

export const SESSION_CATEGORIES = ['teach', 'perform', 'expertise', 'community', 'music'] as const;
