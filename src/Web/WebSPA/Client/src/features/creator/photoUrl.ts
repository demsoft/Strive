import appSettings from 'src/config';

/**
 * The picture of a creator. The version is part of the address, so a new photo is not hidden by a cache; without a
 * version there is no photo (undefined: the initials are shown).
 */
export const creatorPhotoUrl = (creatorIdOrSlug: string | null | undefined, version?: string | null) =>
   creatorIdOrSlug && version
      ? `${appSettings.conferenceUrl.replace(/\/+$/, '')}/v1/creators/${encodeURIComponent(
           creatorIdOrSlug,
        )}/photo?v=${encodeURIComponent(version)}`
      : undefined;
