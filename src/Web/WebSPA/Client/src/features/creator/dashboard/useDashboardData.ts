import { useEffect, useState } from 'react';
import { fetchMyEarnings } from 'src/services/api/creators';
import { fetchMyFollowers } from 'src/services/api/follows';
import { fetchMySessions } from 'src/services/api/sessions';
import { fetchMyVideos } from 'src/services/api/videos';

export type DashboardData = {
   /** each part is missing until it is known, and stays missing if it could not be loaded */
   followers?: number;
   videos?: { ready: number; total: number };
   sessions?: { active: number; total: number };
   earnings?: { netMinor: number; grossMinor: number; sales: number; currency: string };
   loading: boolean;
};

/**
 * The numbers on the dashboard of a creator. They are asked for one by one: one that fails leaves the others as they
 * are (a card then shows a dash). The earnings are only there for somebody whose bank account is saved.
 */
export default function useDashboardData(creatorId: string | undefined, canSell: boolean): DashboardData {
   const [data, setData] = useState<DashboardData>({ loading: true });

   useEffect(() => {
      if (!creatorId) return;
      let cancelled = false;
      setData({ loading: true });

      (async () => {
         const [followers, videos, sessions, earnings] = await Promise.allSettled([
            fetchMyFollowers(0, 1),
            fetchMyVideos(),
            fetchMySessions(),
            canSell ? fetchMyEarnings() : Promise.reject(new Error('no earnings yet')),
         ]);
         if (cancelled) return;

         const now = Date.now();
         setData({
            loading: false,
            followers: followers.status === 'fulfilled' && followers.value ? followers.value.total : undefined,
            videos:
               videos.status === 'fulfilled'
                  ? { ready: videos.value.filter((x) => x.status === 'ready').length, total: videos.value.length }
                  : undefined,
            sessions:
               sessions.status === 'fulfilled'
                  ? {
                       total: sessions.value.length,
                       // on now, or coming (a session without a time stays until it is called off)
                       active: sessions.value.filter(
                          (x) =>
                             x.status === 'published' &&
                             (!x.startsAt || Date.parse(x.startsAt) + x.durationMinutes * 60_000 >= now),
                       ).length,
                    }
                  : undefined,
            earnings:
               earnings.status === 'fulfilled'
                  ? {
                       netMinor: earnings.value.totals.netMinor,
                       grossMinor: earnings.value.totals.grossMinor,
                       sales:
                          earnings.value.totals.tickets +
                          earnings.value.totals.passes +
                          (earnings.value.totals.videos ?? 0),
                       currency: earnings.value.currency,
                    }
                  : undefined,
         });
      })();

      return () => {
         cancelled = true;
      };
   }, [creatorId, canSell]);

   return data;
}
