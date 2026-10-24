import Axios from 'axios';
import { useLayoutEffect } from 'react';
import { useAuth } from 'react-oidc-context';

/**
 * Lets the pages that anybody can open (a creator, a video, the reels, the creators) call the API as the person who is
 * signed in: following, liking, commenting and reporting need the access token, and so do the answers that depend on who
 * asks (who is followed, what is liked, what is locked). The signed in app sets the same header itself.
 *
 * A layout effect: it runs before the effects of the pages below, which already request the API.
 */
export default function AxiosAuth() {
   const auth = useAuth();
   const token = auth.user?.access_token;

   useLayoutEffect(() => {
      if (token) {
         Axios.defaults.headers.common = { ...Axios.defaults.headers.common, Authorization: `Bearer ${token}` };
      } else if (!auth.isLoading) {
         const { Authorization, ...rest } = Axios.defaults.headers.common as Record<string, unknown>;
         void Authorization;
         Axios.defaults.headers.common = rest as typeof Axios.defaults.headers.common;
      }
   }, [token, auth.isLoading]);

   return null;
}
