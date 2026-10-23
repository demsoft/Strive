import { expect, test } from 'vitest';
import { creatorPhotoUrl } from './photoUrl';

test('without a version there is no photo, so the initials are shown', () => {
   expect(creatorPhotoUrl('c1', null)).toBeUndefined();
   expect(creatorPhotoUrl('c1', undefined)).toBeUndefined();
   expect(creatorPhotoUrl('c1', '')).toBeUndefined();
   expect(creatorPhotoUrl(null, '123')).toBeUndefined();
});

test('the version is part of the address, so that a new photo is not hidden by a cache', () => {
   const first = creatorPhotoUrl('c1', '111')!;
   const second = creatorPhotoUrl('c1', '222')!;

   expect(first).toContain('/v1/creators/c1/photo?v=111');
   expect(second).not.toEqual(first);
});

test('the id or name is made safe for an address', () => {
   expect(creatorPhotoUrl('a/b?c', '1')).toContain('/v1/creators/a%2Fb%3Fc/photo');
});
