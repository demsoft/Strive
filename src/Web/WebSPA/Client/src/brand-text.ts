import { isArena } from './brand-id';

/**
 * The translation key to use on a creator platform page. Arena says some things in its own words (football instead of
 * music): its text is in the group "arena" as "<group>_<key>" (landing.fans_title becomes arena.landing_fans_title). Where
 * Arena has no text of its own, the key of Gbedu is used.
 */
export function brandKey(key: string): string | string[] {
   return isArena ? [`arena.${key.replace('.', '_')}`, key] : key;
}
