import Axios, { AxiosAdapter } from 'axios';
import { expect, test, vi } from 'vitest';
import { refreshTokenOnUnauthorized } from './unauthorized';

function createClient(tokenIsValid: (header: string | undefined) => boolean) {
   const client = Axios.create();
   const adapter: AxiosAdapter = async (config) => {
      const header = config.headers?.Authorization as string | undefined;
      if (!tokenIsValid(header)) {
         throw Object.assign(new Error('unauthorized'), {
            isAxiosError: true,
            config,
            response: { status: 401, statusText: 'Unauthorized', data: {}, headers: {}, config },
         });
      }
      return { status: 200, statusText: 'OK', data: { ok: true }, headers: {}, config };
   };
   client.defaults.adapter = adapter;
   client.defaults.headers.common = { Authorization: 'Bearer old' };
   return client;
}

test('a refused token is renewed once and the request repeated with the new one', async () => {
   const client = createClient((h) => h === 'Bearer new');
   const renew = vi.fn(async () => 'new');
   refreshTokenOnUnauthorized(client, { renew, onSessionLost: vi.fn() });

   const response = await client.get('/x');

   expect(response.data).toEqual({ ok: true });
   expect(renew).toHaveBeenCalledTimes(1);
});

test('requests that fail together share one renewal', async () => {
   const client = createClient((h) => h === 'Bearer new');
   const renew = vi.fn(async () => {
      await new Promise((resolve) => setTimeout(resolve, 10));
      return 'new';
   });
   refreshTokenOnUnauthorized(client, { renew, onSessionLost: vi.fn() });

   await Promise.all([client.get('/a'), client.get('/b'), client.get('/c')]);

   expect(renew).toHaveBeenCalledTimes(1);
});

test('when no new token can be had the session is lost and the error stays', async () => {
   const client = createClient(() => false);
   const onSessionLost = vi.fn();
   refreshTokenOnUnauthorized(client, { renew: async () => undefined, onSessionLost });

   await expect(client.get('/x')).rejects.toMatchObject({ response: { status: 401 } });
   expect(onSessionLost).toHaveBeenCalledTimes(1);
});

test('a new token that is refused as well does not loop', async () => {
   const client = createClient(() => false);
   const renew = vi.fn(async () => 'new');
   refreshTokenOnUnauthorized(client, { renew, onSessionLost: vi.fn() });

   await expect(client.get('/x')).rejects.toMatchObject({ response: { status: 401 } });
   expect(renew).toHaveBeenCalledTimes(1);
});
