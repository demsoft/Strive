import { describe, expect, test } from 'vitest';
import { cleanAccountNumber, isCompleteAccountNumber } from './accountNumber';

describe('cleanAccountNumber', () => {
   test('keeps only digits (numbers are pasted with spaces and dashes)', () => {
      expect(cleanAccountNumber('0123 456-789')).toBe('0123456789');
      expect(cleanAccountNumber('abc012')).toBe('012');
   });

   test('at most ten digits', () => {
      expect(cleanAccountNumber('012345678901234')).toBe('0123456789');
   });
});

describe('isCompleteAccountNumber', () => {
   test('exactly ten digits', () => {
      expect(isCompleteAccountNumber('0123456789')).toBe(true);
      expect(isCompleteAccountNumber('012345678')).toBe(false);
      expect(isCompleteAccountNumber('')).toBe(false);
   });
});
