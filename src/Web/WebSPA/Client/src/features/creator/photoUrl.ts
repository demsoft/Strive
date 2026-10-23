import appSettings from 'src/config';

/** The sizes pictures are asked in (the server makes only these: few versions at the image service). */
export const PHOTO_BUCKETS = [64, 128, 256, 400] as const;

/** The smallest size that still looks sharp on a screen with twice the pixels (a phone), at most the biggest. */
export const photoBucket = (cssPixels: number) =>
   PHOTO_BUCKETS.find((bucket) => bucket >= cssPixels * 2) ?? PHOTO_BUCKETS[PHOTO_BUCKETS.length - 1];

/**
 * The picture of a creator for a circle of this many pixels. The version is part of the address, so a new photo is not
 * hidden by a cache; without a version there is no photo (undefined: the initials are shown).
 */
export const creatorPhotoUrl = (
   creatorIdOrSlug: string | null | undefined,
   version: string | null | undefined,
   cssPixels: number,
) =>
   creatorIdOrSlug && version
      ? `${appSettings.conferenceUrl.replace(/\/+$/, '')}/v1/creators/${encodeURIComponent(
           creatorIdOrSlug,
        )}/photo?v=${encodeURIComponent(version)}&w=${photoBucket(cssPixels)}`
      : undefined;
