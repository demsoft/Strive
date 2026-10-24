import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({
   fetchPaymentsConfig: vi.fn(),
   fetchBanks: vi.fn(),
   fetchMyCreatorProfile: vi.fn(),
   resolveAccount: vi.fn(),
   saveMyCreatorProfile: vi.fn(),
}));

vi.mock('react-i18next', () => ({
   useTranslation: () => ({
      t: (key: string, values?: Record<string, unknown>) => (values ? `${key} ${JSON.stringify(values)}` : key),
   }),
}));
vi.mock('react-router-dom', () => ({
   Link: React.forwardRef<HTMLAnchorElement, { to: string; children: React.ReactNode; id?: string }>(
      ({ to, children, id }, ref) => (
         <a ref={ref} href={to} id={id}>
            {children}
         </a>
      ),
   ),
}));
vi.mock('src/features/creators/SiteHeader', () => ({ default: () => null }));
const dashboard = vi.hoisted(() => ({
   data: { loading: false } as Record<string, unknown>,
}));
vi.mock('../dashboard/useDashboardData', () => ({ default: () => dashboard.data }));
vi.mock('src/services/api/creators', () => api);
vi.mock('../errors', () => ({ requestErrorMessage: (e: unknown) => String((e as Error).message ?? e) }));

import CreatorPage from './CreatorPage';

let container: HTMLDivElement;

const settle = () => act(async () => new Promise((resolve) => setTimeout(resolve, 0)));

async function render() {
   await act(async () => createRoot(container).render(<CreatorPage />));
   await settle();
}

const typeInto = async (selector: string, value: string) => {
   const input = document.querySelector(selector) as HTMLInputElement;
   const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!;
   await act(async () => {
      setter.call(input, value);
      input.dispatchEvent(new Event('input', { bubbles: true }));
   });
};

beforeEach(() => {
   Object.values(api).forEach((fn) => fn.mockReset());
   dashboard.data = { loading: false };
   api.fetchPaymentsConfig.mockResolvedValue({
      enabled: true,
      country: 'NG',
      currency: 'NGN',
      platformCommissionPercent: 10,
   });
   api.fetchBanks.mockResolvedValue([{ code: '058', name: 'Guaranty Trust Bank' }]);
   api.fetchMyCreatorProfile.mockResolvedValue(null);
   container = document.createElement('div');
   document.body.appendChild(container);
});
afterEach(() => {
   container.remove();
   document.body.innerHTML = '';
});

test('while paid sessions are not open the page says so and offers no form', async () => {
   api.fetchPaymentsConfig.mockResolvedValue({
      enabled: false,
      country: 'NG',
      currency: 'NGN',
      platformCommissionPercent: 10,
   });

   await render();

   expect(document.querySelector('#creator-not-enabled')).not.toBeNull();
   expect(document.querySelector('#creator-save')).toBeNull();
   expect(api.fetchBanks).not.toHaveBeenCalled();
});

test('a new creator cannot save without a verified bank account', async () => {
   await render();
   await typeInto('#creator-name', 'Ada Lovelace');

   expect((document.querySelector('#creator-save') as HTMLButtonElement).disabled).toBe(true);
   expect(document.querySelector('#creator-account-form')).not.toBeNull();
});

test('an existing creator with a saved account can change the name without the bank details', async () => {
   api.fetchMyCreatorProfile.mockResolvedValue({
      creatorId: 'c1',
      displayName: 'Ada',
      bio: 'Piano',
      country: 'NG',
      currency: 'NGN',
      bankName: 'GTB',
      accountName: 'ADA LOVELACE',
      accountLast4: '6789',
      canSell: true,
      createdAt: '2026-10-19T00:00:00Z',
   });
   api.saveMyCreatorProfile.mockImplementation(async (r) => ({
      creatorId: 'c1',
      ...r,
      canSell: true,
      country: 'NG',
      currency: 'NGN',
      createdAt: '',
   }));
   await render();

   expect(document.querySelector('#creator-account-saved')).not.toBeNull();
   await typeInto('#creator-name', 'Ada L.');
   await act(async () => (document.querySelector('#creator-save') as HTMLElement).click());
   await settle();

   expect(api.saveMyCreatorProfile).toHaveBeenCalledWith({ displayName: 'Ada L.', bio: 'Piano' });
   expect(document.querySelector('#creator-saved')).not.toBeNull();
});

test('the page shows the error of the server when saving fails', async () => {
   api.fetchMyCreatorProfile.mockResolvedValue({
      creatorId: 'c1',
      displayName: 'Ada',
      country: 'NG',
      currency: 'NGN',
      bankName: 'GTB',
      accountName: 'ADA LOVELACE',
      accountLast4: '6789',
      canSell: true,
      createdAt: '',
   });
   api.saveMyCreatorProfile.mockRejectedValue(new Error('The payment service did not answer.'));
   await render();

   await act(async () => (document.querySelector('#creator-save') as HTMLElement).click());
   await settle();

   expect(document.querySelector('#creator-save-error')?.textContent).toContain('did not answer');
});

const savedProfile = (over: Record<string, unknown> = {}) => ({
   creatorId: 'c1',
   displayName: 'Ada Lovelace',
   bio: 'Piano',
   country: 'NG',
   currency: 'NGN',
   bankCode: '058',
   bankName: 'Guaranty Trust Bank',
   accountLast4: '6789',
   accountName: 'ADA LOVELACE',
   canSell: true,
   createdAt: '2026-10-01T10:00:00Z',
   ...over,
});
const text = (selector: string) => document.querySelector(selector)?.textContent ?? '';

test('a creator who can sell gets the dashboard: the numbers, the places to go and the settings', async () => {
   api.fetchMyCreatorProfile.mockResolvedValue(savedProfile());
   dashboard.data = {
      loading: false,
      followers: 2400,
      videos: { ready: 2, total: 3 },
      sessions: { active: 1, total: 4 },
      earnings: { netMinor: 1665000, grossMinor: 1850000, sales: 14, currency: 'NGN' },
   };

   await render();

   expect(document.querySelector('#creator-dashboard')).not.toBeNull();
   expect(text('#creator-hero-name')).toBe('Ada Lovelace');
   expect(text('#creator-status')).toContain('dashboard.status_ready');
   expect(text('#stat-followers .stat-value')).toMatch(/2\.4/);
   expect(text('#stat-videos .stat-value')).toBe('2');
   expect(text('#stat-videos .stat-hint')).toContain('"count":1');
   expect(text('#stat-sessions .stat-value')).toBe('1');
   expect(text('#stat-earnings .stat-value')).toContain('16,650');
   expect(document.querySelector('#creator-sessions-link')?.getAttribute('href')).toBe('/creator/sessions');
   expect(document.querySelector('#creator-videos-link')?.getAttribute('href')).toBe('/creator/videos');
   expect(document.querySelector('#creator-earnings-link')?.getAttribute('href')).toBe('/creator/earnings');
   expect(document.querySelector('#creator-followers-link')?.getAttribute('href')).toBe('/connections?tab=followers');
   expect(document.querySelector('#creator-public-link')?.getAttribute('href')).toBe('/u/c1');
   expect(document.querySelector('#photo-setup')).not.toBeNull();
   expect(document.querySelector('#slug-setup')).not.toBeNull();
   expect(document.querySelector('#pass-setup')).not.toBeNull();
});

test('somebody without a verified bank account sees sessions and earnings as locked, no sales settings and no earnings number', async () => {
   api.fetchMyCreatorProfile.mockResolvedValue(savedProfile({ canSell: false, bankName: null }));
   dashboard.data = { loading: false, videos: { ready: 0, total: 0 } };

   await render();

   expect(text('#creator-status')).toContain('dashboard.status_setup');
   // locked cards are buttons, not links
   expect(document.querySelector('#creator-sessions-link')?.tagName).toBe('BUTTON');
   expect(document.querySelector('#creator-earnings-link')?.tagName).toBe('BUTTON');
   expect(document.querySelector('#creator-videos-link')?.tagName).toBe('A');
   expect(document.querySelector('#creator-public-link')).toBeNull();
   expect(document.querySelector('#slug-setup')).toBeNull();
   expect(document.querySelector('#pass-setup')).toBeNull();
   expect(text('#stat-earnings .stat-value')).toBe('—');
});

test('the checklist counts what is done and leads to what is left', async () => {
   api.fetchMyCreatorProfile.mockResolvedValue(savedProfile());
   dashboard.data = { loading: false, videos: { ready: 0, total: 0 }, sessions: { active: 0, total: 0 } };

   await render();

   const open = [...document.querySelectorAll('.setup-step.open')].length;
   expect(open).toBeGreaterThanOrEqual(3); // photo, video, session, link, pass
   expect(document.querySelectorAll('.setup-step.done')).toHaveLength(1); // the bank account
});

test('a new creator sees only the way in, no dashboard', async () => {
   await render();

   expect(document.querySelector('#creator-onboarding')).not.toBeNull();
   expect(document.querySelector('#creator-dashboard')).toBeNull();
});
