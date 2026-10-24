/** 1200 is "1.2K": numbers on cards stay short. */
export const compactCount = (n: number) => {
   try {
      return new Intl.NumberFormat(undefined, { notation: 'compact', maximumFractionDigits: 1 }).format(n);
   } catch {
      return String(n);
   }
};
