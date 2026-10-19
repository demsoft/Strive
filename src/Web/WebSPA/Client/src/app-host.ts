/** The addresses the server tells the web app (see config.ts), without a trailing slash. */
export type HostAddresses = {
   frontendUrl: string;
   identityUrl: string;
};

/**
 * The web app may answer on more than one address (another brand on its own host name, for example gbedu.example.com
 * next to meet.example.com), all served by the same server. The server tells the addresses of the main host; on
 * another host the app and the sign in pages (which are under /account of the host of the app in production) have to
 * be reached through that host: the person must never be sent to a host name they did not come from.
 *
 * In development the sign in pages are on a host of their own: nothing is changed there.
 */
export function adaptToHost<T extends HostAddresses>(settings: T, currentOrigin: string): T {
   let main: URL;
   let identity: URL;
   try {
      main = new URL(settings.frontendUrl);
      identity = new URL(settings.identityUrl);
   } catch {
      return settings;
   }

   if (currentOrigin === main.origin) return settings;
   if (identity.origin !== main.origin) return settings;

   return {
      ...settings,
      frontendUrl: currentOrigin,
      identityUrl: currentOrigin + identity.pathname.replace(/\/$/, ''),
   };
}

/** True if the web app is opened on an address other than the main one, e.g. a second brand. */
export function isOtherHost(settings: HostAddresses, currentOrigin: string): boolean {
   try {
      return new URL(settings.frontendUrl).origin !== currentOrigin;
   } catch {
      return false;
   }
}
