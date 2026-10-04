import { useCallback, useEffect, useRef, useState } from 'react';
import { AdminOverview, AdminSample, fetchHistory, fetchOverview } from './api';

const OVERVIEW_REFRESH_MS = 5000;
const HISTORY_REFRESH_MS = 60000;

export type AdminData = {
   overview: AdminOverview | null;
   history: AdminSample[];
   /** 403: the signed in user is no server administrator */
   forbidden: boolean;
   error: string | null;
   loadedAt: number | null;
   refresh: () => void;
};

const statusOf = (error: unknown) => (error as { response?: { status?: number } })?.response?.status;

/** The numbers of the admin overview, refreshed every few seconds while the page is open and visible. */
export default function useAdminData(historyHours: number): AdminData {
   const [overview, setOverview] = useState<AdminOverview | null>(null);
   const [history, setHistory] = useState<AdminSample[]>([]);
   const [forbidden, setForbidden] = useState(false);
   const [error, setError] = useState<string | null>(null);
   const [loadedAt, setLoadedAt] = useState<number | null>(null);
   const cancelled = useRef(false);

   const loadOverview = useCallback(async () => {
      if (document.hidden) return;

      try {
         const result = await fetchOverview();
         if (cancelled.current) return;
         setOverview(result);
         setError(null);
         setForbidden(false);
         setLoadedAt(Date.now());
      } catch (e) {
         if (cancelled.current) return;
         if (statusOf(e) === 403) setForbidden(true);
         else setError('The server did not answer.');
      }
   }, []);

   const loadHistory = useCallback(async () => {
      if (document.hidden) return;

      try {
         const result = await fetchHistory(historyHours);
         if (!cancelled.current) setHistory(result);
      } catch {
         // the charts are optional, the overview shows the problem
      }
   }, [historyHours]);

   useEffect(() => {
      cancelled.current = false;
      loadOverview();
      const timer = setInterval(loadOverview, OVERVIEW_REFRESH_MS);
      return () => {
         cancelled.current = true;
         clearInterval(timer);
      };
   }, [loadOverview]);

   useEffect(() => {
      loadHistory();
      const timer = setInterval(loadHistory, HISTORY_REFRESH_MS);
      return () => clearInterval(timer);
   }, [loadHistory]);

   const refresh = useCallback(() => {
      loadOverview();
      loadHistory();
   }, [loadOverview, loadHistory]);

   return { overview, history, forbidden, error, loadedAt, refresh };
}
