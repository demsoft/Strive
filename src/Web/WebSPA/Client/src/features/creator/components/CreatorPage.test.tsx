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
vi.mock('react-router-dom', () => ({ Link: ({ children }: { children: React.ReactNode }) => <a>{children}</a> }));
vi.mock('src/components/BrandLogo', () => ({ default: () => null }));
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
