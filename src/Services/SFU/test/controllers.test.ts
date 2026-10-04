import express from 'express';
import { AddressInfo } from 'net';
import { Server } from 'http';
import configureEndpoints from '../src/controllers';
import ConferenceManager from '../src/lib/conference/conference-manager';

describe('controllers', () => {
   let server: Server;
   let baseUrl: string;

   beforeAll(async () => {
      const app = express();
      configureEndpoints(app, {} as unknown as ConferenceManager);
      await new Promise<void>((resolve) => {
         server = app.listen(0, () => resolve());
      });
      baseUrl = `http://127.0.0.1:${(server.address() as AddressInfo).port}`;
   });

   afterAll(() => new Promise<void>((resolve) => server.close(() => resolve())));

   it('answers the CORS preflight of the browser without a token', async () => {
      const response = await fetch(`${baseUrl}/conf1/init-connection`, {
         method: 'OPTIONS',
         headers: {
            Origin: 'https://meet.example.com',
            'Access-Control-Request-Method': 'POST',
            'Access-Control-Request-Headers': 'authorization,content-type',
         },
      });

      expect(response.status).toBe(204);
      expect(response.headers.get('access-control-allow-origin')).toBeTruthy();
      expect(response.headers.get('access-control-allow-headers')?.toLowerCase()).toContain('authorization');
   });

   it('still requires a token for the real request', async () => {
      const response = await fetch(`${baseUrl}/conf1/init-connection`, {
         method: 'POST',
         headers: { Origin: 'https://meet.example.com', 'content-type': 'application/json' },
         body: '{}',
      });

      expect(response.status).toBe(401);
   });
});

describe('admin stats', () => {
   let server: Server;
   let baseUrl: string;
   const stats = { totals: { participants: 3 } };

   beforeAll(async () => {
      const app = express();
      configureEndpoints(app, {} as unknown as ConferenceManager, { apiKey: 'secret-key', getStats: async () => stats });
      await new Promise<void>((resolve) => {
         server = app.listen(0, () => resolve());
      });
      baseUrl = `http://127.0.0.1:${(server.address() as AddressInfo).port}`;
   });

   afterAll(() => new Promise<void>((resolve) => server.close(() => resolve())));

   it('returns the stats for the api key', async () => {
      const response = await fetch(`${baseUrl}/admin/stats`, { headers: { 'x-api-key': 'secret-key' } });

      expect(response.status).toBe(200);
      expect(await response.json()).toEqual(stats);
   });

   it.each([[undefined], ['wrong'], ['secret-ke'], ['secret-keyx']])('refuses the api key %s', async (key) => {
      const headers: Record<string, string> = key === undefined ? {} : { 'x-api-key': key };

      const response = await fetch(`${baseUrl}/admin/stats`, { headers });

      expect(response.status).toBe(401);
   });

   it('does not exist without an api key', async () => {
      const app = express();
      configureEndpoints(app, {} as unknown as ConferenceManager, { apiKey: undefined, getStats: async () => stats });
      const local = await new Promise<Server>((resolve) => {
         const s = app.listen(0, () => resolve(s));
      });
      const url = `http://127.0.0.1:${(local.address() as AddressInfo).port}`;

      // falls through to the token check of the other endpoints
      const response = await fetch(`${url}/admin/stats`, { headers: { 'x-api-key': 'anything' } });
      await new Promise<void>((resolve) => local.close(() => resolve()));

      expect(response.status).toBe(401);
   });
});
