import React, { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({
   fetchMyEarnings: vi.fn(),
   fetchMyFollowers: vi.fn(),
   fetchMySessions: vi.fn(),
   fetchMyVideos: vi.fn(),
}));
vi.mock('src/services/api/creators', () => ({ fetchMyEarnings: api.fetchMyEarnings }));
vi.mock('src/services/api/follows', () => ({ fetchMyFollowers: api.fetchMyFollowers }));
vi.mock('src/services/api/sessions', () => ({ fetchMySessions: api.fetchMySessions }));
vi.mock('src/services/api/videos', () => ({ fetchMyVideos: api.fetchMyVideos }));

import useDashboardData, { DashboardData } from './useDashboardData';

let current: DashboardData;
const Probe = ({ id = 'c1', canSell = true }: { id?: string; canSell?: boolean }) => {
   current = useDashboardData(id, canSell);
   return null;
};

let container: HTMLDivElement;
let root: Root;
const render = async (props: { id?: string; canSell?: boolean } = {}) => {
   await act(async () => root.render(<Probe {...props} />));
   await act(async () => undefined);
};

const inAnHour = () => new Date(Date.now() + 3_600_000).toISOString();
const anHourAgo = () => new Date(Date.now() - 3_600_000).toISOString();
const dayAgo = () => new Date(Date.now() - 86_400_000).toISOString();

beforeEach(() => {
   container = document.createElement('div');
   document.body.appendChild(container);
   root = createRoot(container);
   api.fetchMyFollowers.mockResolvedValue({ items: [], total: 12 });
   api.fetchMyVideos.mockResolvedValue([{ status: 'ready' }, { status: 'ready' }, { status: 'processing' }]);
   api.fetchMySessions.mockResolvedValue([
      { status: 'published', startsAt: inAnHour(), durationMinutes: 60 },
      { status: 'published', startsAt: null, durationMinutes: 60 },
      { status: 'published', startsAt: dayAgo(), durationMinutes: 60 },
      { status: 'cancelled', startsAt: inAnHour(), durationMinutes: 60 },
      { status: 'published', startsAt: anHourAgo(), durationMinutes: 180 },
   ]);
   api.fetchMyEarnings.mockResolvedValue({
      currency: 'NGN',
      totals: {
         grossMinor: 2000,
         platformMinor: 200,
         netMinor: 1800,
         refundedMinor: 0,
         tickets: 3,
         passes: 1,
         refunds: 0,
      },
   });
});
afterEach(() => {
   act(() => root.unmount());
   container.remove();
   vi.clearAllMocks();
});

test('counts what is on and what is ready, and adds up the sales', async () => {
   await render();

   expect(current.loading).toBe(false);
   expect(current.followers).toBe(12);
   expect(current.videos).toEqual({ ready: 2, total: 3 });
   // coming, without a time, and one that began an hour ago and lasts three: not the one of yesterday or the called off
   expect(current.sessions).toEqual({ active: 3, total: 5 });
   expect(current.earnings).toEqual({ netMinor: 1800, grossMinor: 2000, sales: 4, currency: 'NGN' });
});

test('what cannot be loaded is missing, the rest is there', async () => {
   api.fetchMyVideos.mockRejectedValue(new Error('down'));
   api.fetchMyFollowers.mockResolvedValue(null);
   await render();

   expect(current.videos).toBeUndefined();
   expect(current.followers).toBeUndefined();
   expect(current.sessions?.total).toBe(5);
});

test('earnings are not asked for before the bank account is saved', async () => {
   await render({ canSell: false });

   expect(api.fetchMyEarnings).not.toHaveBeenCalled();
   expect(current.earnings).toBeUndefined();
});

test('nothing is asked for before the creator is known', async () => {
   await act(async () => root.render(<Probe id="" />));

   expect(api.fetchMyVideos).not.toHaveBeenCalled();
   expect(current.loading).toBe(true);
});
