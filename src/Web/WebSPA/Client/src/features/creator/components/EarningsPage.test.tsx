import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({ fetchMyEarnings: vi.fn() }));

vi.mock('react-i18next', () => ({
   useTranslation: () => ({
      t: (key: string, values?: Record<string, unknown>) => (values ? `${key} ${JSON.stringify(values)}` : key),
   }),
}));
vi.mock('react-router-dom', () => ({
   Link: ({ children }: { children: React.ReactNode }) => <a>{children}</a>,
}));
vi.mock('src/components/BrandLogo', () => ({ default: () => null }));
vi.mock('src/services/api/creators', () => api);
vi.mock('../errors', () => ({ requestErrorMessage: (e: unknown) => String((e as Error).message ?? e) }));

import EarningsPage from './EarningsPage';

const earnings = (over: Record<string, unknown> = {}) => ({
   currency: 'NGN',
   totals: {
      grossMinor: 1000000,
      platformMinor: 100000,
      netMinor: 900000,
      refundedMinor: 500000,
      tickets: 2,
      passes: 1,
      refunds: 1,
   },
   sessions: [{ sessionId: 'a', title: 'Piano', tickets: 2, grossMinor: 1000000, netMinor: 900000 }],
   recent: [
      {
         at: '2026-10-01T12:00:00Z',
         kind: 'ticket',
         sessionTitle: 'Piano',
         amountMinor: 500000,
         netMinor: 450000,
         buyer: 'f**@example.com',
         refunded: false,
      },
      {
         at: '2026-10-01T11:00:00Z',
         kind: 'pass',
         sessionTitle: null,
         amountMinor: 2000000,
         netMinor: 1800000,
         buyer: 'a**@example.com',
         refunded: false,
      },
      {
         at: '2026-10-01T10:00:00Z',
         kind: 'ticket',
         sessionTitle: 'Piano',
         amountMinor: 500000,
         netMinor: 450000,
         buyer: 'k**@example.com',
         refunded: true,
      },
   ],
   ...over,
});

const settle = () => act(async () => {});

let container: HTMLDivElement;
beforeEach(() => {
   container = document.createElement('div');
   document.body.appendChild(container);
});
afterEach(() => {
   container.remove();
   document.body.innerHTML = '';
   vi.clearAllMocks();
});

test('shows the share of the creator first, then gross, commission and refunds', async () => {
   api.fetchMyEarnings.mockResolvedValue(earnings());
   await act(async () => createRoot(container).render(<EarningsPage />));
   await settle();

   expect(document.querySelector('#earn-net')?.textContent).toContain('₦9,000');
   expect(document.querySelector('#earn-gross')?.textContent).toContain('₦10,000');
   expect(document.querySelector('#earn-fee')?.textContent).toContain('₦1,000');
   expect(document.querySelector('#earn-refunded')?.textContent).toContain('₦5,000');
   expect(document.querySelector('#earn-counts')?.textContent).toContain('"tickets":2');
});

test('lists the sessions and the latest sales, a refunded sale is marked', async () => {
   api.fetchMyEarnings.mockResolvedValue(earnings());
   await act(async () => createRoot(container).render(<EarningsPage />));
   await settle();

   expect(document.querySelectorAll('.earn-session')).toHaveLength(1);
   const sales = document.querySelectorAll('.earn-sale');
   expect(sales).toHaveLength(3);
   expect(sales[0].textContent).toContain('f**@example.com');
   expect(sales[1].textContent).toContain('creator.pass_title');
   expect(sales[2].querySelector('.earn-refunded')).not.toBeNull();
   expect(sales[0].querySelector('.earn-refunded')).toBeNull();
});

test('no sales yet: says so and shows the payout note', async () => {
   api.fetchMyEarnings.mockResolvedValue(
      earnings({
         totals: { grossMinor: 0, platformMinor: 0, netMinor: 0, refundedMinor: 0, tickets: 0, passes: 0, refunds: 0 },
         sessions: [],
         recent: [],
      }),
   );
   await act(async () => createRoot(container).render(<EarningsPage />));
   await settle();

   expect(document.querySelector('#earn-nothing')).not.toBeNull();
   expect(document.querySelector('#earn-sessions')).toBeNull();
   expect(document.querySelector('#earn-payout-note')).not.toBeNull();
});

test('a server error is shown instead of zeros', async () => {
   api.fetchMyEarnings.mockRejectedValue(new Error('There is no creator profile.'));
   await act(async () => createRoot(container).render(<EarningsPage />));
   await settle();

   expect(document.querySelector('#earnings-page')?.textContent).toContain('There is no creator profile.');
   expect(document.querySelector('#earn-net')).toBeNull();
});
