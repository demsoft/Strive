import { describe, expect, test } from 'vitest';
import { creatorPhotoUrl, photoBucket } from './photoUrl';

describe('photoBucket', () => {
   test.each([
      [22, 64],
      [32, 64],
      [33, 128],
      [64, 128],
      [72, 256],
      [80, 256],
      [128, 256],
      [129, 400],
      [1000, 400],
   ])('a circle of %i pixels gets the %i picture (twice the pixels for a sharp phone screen)', (pixels, bucket) => {
      expect(photoBucket(pixels)).toBe(bucket);
   });
});

test('without a version there is no photo, so the initials are shown', () => {
   expect(creatorPhotoUrl('c1', null, 40)).toBeUndefined();
   expect(creatorPhotoUrl('c1', undefined, 40)).toBeUndefined();
   expect(creatorPhotoUrl('c1', '', 40)).toBeUndefined();
   expect(creatorPhotoUrl(null, '123', 40)).toBeUndefined();
});

test('the version is part of the address, so that a new photo is not hidden by a cache', () => {
   const first = creatorPhotoUrl('c1', '111', 40)!;
   const second = creatorPhotoUrl('c1', '222', 40)!;

   expect(first).toContain('/v1/creators/c1/photo?v=111');
   expect(second).not.toEqual(first);
});

test('the size is asked in the address, so that a small circle does not load a big picture', () => {
   expect(creatorPhotoUrl('c1', '1', 22)).toContain('&w=64');
   expect(creatorPhotoUrl('c1', '1', 80)).toContain('&w=256');
});

test('the id or name is made safe for an address', () => {
   expect(creatorPhotoUrl('a/b?c', '1', 40)).toContain('/v1/creators/a%2Fb%3Fc/photo');
});
