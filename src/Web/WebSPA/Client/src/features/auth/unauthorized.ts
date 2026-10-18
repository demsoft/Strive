import type { AxiosError, AxiosInstance, AxiosRequestConfig } from 'axios';

type Options = {
   /** gets a new access token (silent sign in), returns undefined if it was not possible */
   renew: () => Promise<string | undefined>;
   /** the session is over: sign in again */
   onSessionLost: () => void;
};

/**
 * When the API refuses the access token (it ran out, for example after the computer slept and the renewal in the
 * background did not happen), a new one is requested once and the request is repeated. Without this, screens that load
 * data stay empty until the person signs out and in again. Several requests that fail together share one renewal.
 */
export function refreshTokenOnUnauthorized(axios: AxiosInstance, { renew, onSessionLost }: Options): () => void {
   let renewing: Promise<string | undefined> | null = null;

   const id = axios.interceptors.response.use(undefined, async (error: AxiosError) => {
      const config = error.config as (AxiosRequestConfig & { _retriedWithNewToken?: boolean }) | undefined;
      if (error.response?.status !== 401 || !config || config._retriedWithNewToken) throw error;

      renewing ??= renew()
         .catch(() => undefined)
         .finally(() => {
            renewing = null;
         });
      const token = await renewing;

      if (!token) {
         onSessionLost();
         throw error;
      }

      axios.defaults.headers.common = { ...axios.defaults.headers.common, Authorization: `Bearer ${token}` };
      config._retriedWithNewToken = true;
      config.headers = { ...config.headers, Authorization: `Bearer ${token}` } as AxiosRequestConfig['headers'];
      return axios.request(config);
   });

   return () => axios.interceptors.response.eject(id);
}
