import { useCallback, useEffect, useState } from 'react';
import * as api from 'src/services/api/follows';

/**
 * Whom the signed in person follows, and a way to follow or stop following. The button reacts at once; if the server
 * refuses, it goes back. Without a token nobody is followed and `toggle` does nothing (the caller asks for a sign in).
 */
export default function useFollowing(token?: string) {
   const [ids, setIds] = useState<ReadonlySet<string>>(new Set());

   useEffect(() => {
      if (!token) {
         setIds(new Set());
         return;
      }
      let cancelled = false;
      api.fetchFollowing().then(
         (creators) => !cancelled && setIds(new Set(creators.map((x) => x.creatorId))),
         () => undefined,
      );
      return () => {
         cancelled = true;
      };
   }, [token]);

   const set = (creatorId: string, following: boolean) =>
      setIds((current) => {
         const next = new Set(current);
         if (following) next.add(creatorId);
         else next.delete(creatorId);
         return next;
      });

   const toggle = useCallback(
      async (creatorId: string) => {
         if (!token) return;
         const was = ids.has(creatorId);
         set(creatorId, !was);
         try {
            await (was ? api.unfollowCreator(creatorId) : api.followCreator(creatorId));
         } catch {
            set(creatorId, was);
         }
      },
      [token, ids],
   );

   return { isFollowing: (creatorId: string) => ids.has(creatorId), toggle };
}
