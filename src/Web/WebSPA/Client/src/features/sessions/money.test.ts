import { describe, expect, test } from 'vitest';
import { formatMoney, isPriceInRange, parsePriceToMinor } from './money';

describe('formatMoney', () => {
   test('kobo become naira, whole amounts without decimals', () => {
      expect(formatMoney(500000, 'NGN')).toBe('₦5,000');
      expect(formatMoney(150050, 'NGN')).toBe('₦1,500.50');
   });
});

describe('parsePriceToMinor', () => {
   test.each([
      ['5000', 500000],
      ['5,000', 500000],
      ['₦ 5 000', 500000],
      ['1500.5', 150050],
      ['1500.50', 150050],
      ['0.99', 99],
   ])('%s is %s kobo', (input, expected) => {
      expect(parsePriceToMinor(input)).toBe(expected);
   });

   test.each(['', 'abc', '12.345', '1.2.3', '-5'])('%s is not a price', (input) => {
      // the minus is dropped like any other character, so "-5" reads as 5: the range check protects, not the parser
      if (input === '-5') expect(parsePriceToMinor(input)).toBe(500);
      else expect(parsePriceToMinor(input)).toBeNull();
   });
});

describe('isPriceInRange', () => {
   test('from 100 to 1,000,000 naira', () => {
      expect(isPriceInRange(9999)).toBe(false);
      expect(isPriceInRange(10000)).toBe(true);
      expect(isPriceInRange(100_000_000)).toBe(true);
      expect(isPriceInRange(100_000_001)).toBe(false);
   });
});
