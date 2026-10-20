import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({ setPassPrice: vi.fn() }));

vi.mock('react-i18next', () => ({
   useTranslation: () => ({
      t: (key: string, values?: Record<string, unknown>) => (values ? `${key} ${JSON.stringify(values)}` : key),
   }),
}));
vi.mock('src/services/api/creators', () => api);
vi.mock('../errors', () => ({ requestErrorMessage: (e: unknown) => String((e as Error).message ?? e) }));

import PassSetup from './PassSetup';

const profile = (over: Record<string, unknown> = {}) =>
   ({ creatorId: 'c1', displayName: 'Ada', currency: 'NGN', canSell: true, ...over } as any);

const type = async (selector: string, value: string) => {
   const input = document.querySelector(selector) as HTMLInputElement;
   const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!;
   await act(async () => {
      setter.call(input, value);
      input.dispatchEvent(new Event('input', { bubbles: true }));
   });
};

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

test('the price is sent in kobo and the new profile is handed on', async () => {
   const updated = profile({ passPriceMinor: 2500000 });
   api.setPassPrice.mockResolvedValue(updated);
   const onSaved = vi.fn();
   await act(async () => createRoot(container).render(<PassSetup profile={profile()} onSaved={onSaved} />));

   const save = document.querySelector('#pass-save') as HTMLButtonElement;
   expect(save.disabled).toBe(true);
   await type('#pass-price', '25,000');
   await act(async () => save.click());

   expect(api.setPassPrice).toHaveBeenCalledWith(2500000);
   expect(onSaved).toHaveBeenCalledWith(updated);
});

test('a creator who sells a pass can stop selling it', async () => {
   api.setPassPrice.mockResolvedValue(profile());
   const onSaved = vi.fn();
   await act(async () =>
      createRoot(container).render(<PassSetup profile={profile({ passPriceMinor: 2000000 })} onSaved={onSaved} />),
   );

   expect((document.querySelector('#pass-price') as HTMLInputElement).value).toBe('20000');
   await act(async () => (document.querySelector('#pass-stop') as HTMLElement).click());

   expect(api.setPassPrice).toHaveBeenCalledWith(null);
});

test('no stop button while no pass is sold, and the server error is shown', async () => {
   api.setPassPrice.mockRejectedValue(new Error('The price must be between 100 and 1000000 NGN.'));
   await act(async () => createRoot(container).render(<PassSetup profile={profile()} onSaved={vi.fn()} />));
   expect(document.querySelector('#pass-stop')).toBeNull();

   await type('#pass-price', '50');
   // the page only checks that it is an amount: the range is the decision of the server, its answer is shown
   expect((document.querySelector('#pass-save') as HTMLButtonElement).disabled).toBe(false);
   await act(async () => (document.querySelector('#pass-save') as HTMLElement).click());

   expect(document.querySelector('#pass-setup-error')?.textContent).toContain('The price must be');
});
