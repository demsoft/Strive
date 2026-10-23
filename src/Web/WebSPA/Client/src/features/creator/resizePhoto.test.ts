import { describe, expect, test } from 'vitest';
import { isAcceptedPhoto, MAX_SOURCE_BYTES, squareCrop } from './resizePhoto';

describe('squareCrop', () => {
   test('a wide picture is cut at the sides, in the middle', () => {
      expect(squareCrop(4000, 3000)).toEqual({ sx: 500, sy: 0, side: 3000 });
   });

   test('a tall picture is cut at the top and the bottom, in the middle', () => {
      expect(squareCrop(3000, 4000)).toEqual({ sx: 0, sy: 500, side: 3000 });
   });

   test('a square stays as it is; odd differences are rounded down', () => {
      expect(squareCrop(500, 500)).toEqual({ sx: 0, sy: 0, side: 500 });
      expect(squareCrop(1001, 1000)).toEqual({ sx: 0, sy: 0, side: 1000 });
   });
});

describe('isAcceptedPhoto', () => {
   test.each(['image/jpeg', 'image/png', 'image/webp'])('%s is accepted', (type) => {
      expect(isAcceptedPhoto({ type, size: 1000 })).toBe(true);
   });

   test.each(['image/svg+xml', 'image/gif', 'text/html', 'application/pdf', ''])('%s is not', (type) => {
      expect(isAcceptedPhoto({ type, size: 1000 })).toBe(false);
   });

   test('empty and too big files are not', () => {
      expect(isAcceptedPhoto({ type: 'image/jpeg', size: 0 })).toBe(false);
      expect(isAcceptedPhoto({ type: 'image/jpeg', size: MAX_SOURCE_BYTES })).toBe(true);
      expect(isAcceptedPhoto({ type: 'image/jpeg', size: MAX_SOURCE_BYTES + 1 })).toBe(false);
   });
});
