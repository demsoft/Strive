import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({ fetchAdminPayments: vi.fn() }));

vi.mock('react-i18next', () => ({
   useTranslation: () => ({
      t: (key: string, values?: Record<string, unknown>) => (values ? `${key} ${JSON.stringify(values)}` : key),
   }),
}));
vi.mock('react-router-dom', () => ({
   Link: ({ children }: { children: React.ReactNode }) => <a>{children}</a>,
}));
vi.mock('src/components/BrandLogo', () => ({ default: () => null }));
vi.mock('../api', () => api);

import AdminPaymentsPage from './AdminPaymentsPage';

const period = (days: number, over: Record<string, unknown> = {}) => ({
   days,
   grossMinor: 1000000,
   platformMinor: 100000,
   creatorsMinor: 900000,
   refundedMinor: 0,
   tickets: 2,
   passes: 1,
   renewals: 0,
   refunds: 0,
   ...over,
});

const payments = (over: Record<string, unknown> = {}) => ({
   currency: 'NGN',
   paymentsEnabled: true,
   autoRenewEnabled: true,
   creators: 4,
   activeSubscriptions: 2,
   week: period(7),
   month: period(30, { grossMinor: 5000000, platformMinor: 500000, creatorsMinor: 4500000 }),
   recent: [
      {
         at: '2026-10-20T10:00:00Z',
         kind: 'ticket',
         status: 'paid',
         creatorName: 'Ada',
         sessionTitle: 'Piano',
         amountMinor: 500000,
         buyer: 'f**@example.com',
         problem: null,
      },
      {
         at: '2026-10-20T09:00:00Z',
         kind: 'renewal',
         status: 'failed',
         creatorName: 'Kofi',
         sessionTitle: null,
         amountMinor: 2000000,
         buyer: 'a**@example.com',
         problem: 'The renewal was refused.',
      },
   ],
   refundProblems: [],
   subscriptionProblems: [],
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

test('shows the totals of the week and the month with the commission apart', async () => {
   api.fetchAdminPayments.mockResolvedValue(payments());
   await act(async () => createRoot(container).render(<AdminPaymentsPage />));
   await settle();

   expect(document.querySelector('#admin-pay-week-gross')?.textContent).toContain('₦10,000');
   expect(document.querySelector('#admin-pay-week-platform')?.textContent).toContain('₦1,000');
   expect(document.querySelector('#admin-pay-month-gross')?.textContent).toContain('₦50,000');
   expect(document.querySelector('#admin-pay-month-creators')?.textContent).toContain('₦45,000');
   expect(document.querySelector('#admin-pay-creators-count')?.textContent).toContain('4');
});

test('when nothing is wrong it says so; the latest orders are listed with their problem', async () => {
   api.fetchAdminPayments.mockResolvedValue(payments());
   await act(async () => createRoot(container).render(<AdminPaymentsPage />));
   await settle();

   expect(document.querySelector('#admin-pay-attention')?.textContent).toContain('admin.pay_all_good');
   const rows = document.querySelectorAll('.order-row');
   expect(rows).toHaveLength(2);
   expect(rows[0].textContent).toContain('Piano');
   expect(rows[1].textContent).toContain('The renewal was refused.');
   expect(rows[1].textContent).toContain('admin.pay_kind_renewal');
});

test('refunds that failed and renewals that fail are put at the top', async () => {
   api.fetchAdminPayments.mockResolvedValue(
      payments({
         refundProblems: [
            {
               reference: 'ref1',
               creatorName: 'Ada',
               sessionTitle: 'Piano',
               amountMinor: 500000,
               buyer: 'f**@example.com',
               reason: 'Insufficient balance.',
               at: '2026-10-20T10:00:00Z',
            },
         ],
         subscriptionProblems: [
            {
               subscriptionId: 's1',
               creatorName: 'Kofi',
               buyer: 'a**@example.com',
               status: 'active',
               failedAttempts: 2,
               nextChargeAt: '2026-10-21T10:00:00Z',
               endedAt: null,
            },
            {
               subscriptionId: 's2',
               creatorName: 'Ngozi',
               buyer: 'k**@example.com',
               status: 'ended',
               failedAttempts: 3,
               nextChargeAt: '2026-10-19T10:00:00Z',
               endedAt: '2026-10-19T10:00:00Z',
            },
         ],
      }),
   );
   await act(async () => createRoot(container).render(<AdminPaymentsPage />));
   await settle();

   expect(document.querySelector('#admin-pay-attention')?.textContent).toContain('"count":3');
   expect(document.querySelector('.refund-problem')?.textContent).toContain('Insufficient balance.');
   expect(document.querySelector('.refund-problem')?.textContent).toContain('₦5,000');
   const subs = document.querySelectorAll('.subscription-problem');
   expect(subs).toHaveLength(2);
   expect(subs[0].textContent).toContain('admin.pay_sub_active');
   expect(subs[1].textContent).toContain('admin.pay_sub_ended');
});

test('a person who is no administrator sees the notice, not the money', async () => {
   api.fetchAdminPayments.mockRejectedValue({ response: { status: 403 } });
   await act(async () => createRoot(container).render(<AdminPaymentsPage />));
   await settle();

   expect(document.querySelector('#admin-pay-forbidden')).not.toBeNull();
   expect(document.querySelector('#admin-payments-page')).toBeNull();
});

test('another failure shows an error; switched off payments are said', async () => {
   api.fetchAdminPayments.mockRejectedValueOnce(new Error('down'));
   await act(async () => createRoot(container).render(<AdminPaymentsPage />));
   await settle();
   expect(document.body.textContent).toContain('admin.pay_failed');

   container.innerHTML = '';
   api.fetchAdminPayments.mockResolvedValue(payments({ paymentsEnabled: false }));
   const second = document.createElement('div');
   document.body.appendChild(second);
   await act(async () => createRoot(second).render(<AdminPaymentsPage />));
   await settle();
   expect(document.querySelector('#admin-pay-disabled')).not.toBeNull();
});
