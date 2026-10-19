import { describe, expect, test } from 'vitest';
import { detectBrandId } from './brand-id';

describe('detectBrandId', () => {
   test('gbedu.* is Gbedu, other addresses are Strive', () => {
      expect(detectBrandId('gbedu.goserp.co.uk')).toBe('gbedu');
      expect(detectBrandId('GBEDU.example.com')).toBe('gbedu');
      expect(detectBrandId('meet.goserp.co.uk')).toBe('strive');
      expect(detectBrandId('localhost')).toBe('strive');
   });

   test('?brand= switches the brand to try the other one without a second address', () => {
      expect(detectBrandId('localhost', '?brand=gbedu')).toBe('gbedu');
      expect(detectBrandId('gbedu.example.com', '?brand=strive')).toBe('strive');
      expect(detectBrandId('localhost', '?brand=other')).toBe('strive');
   });

   test('the choice made with ?brand= is remembered for the next page', () => {
      expect(detectBrandId('localhost', '', 'gbedu')).toBe('gbedu');
      expect(detectBrandId('localhost', '', null)).toBe('strive');
   });
});
