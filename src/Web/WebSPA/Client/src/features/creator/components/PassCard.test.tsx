import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({ fetchPayment: vi.fn(), startPassCheckout: vi.fn() }));
const state = vi.hoisted(() => ({
   user: undefined as undefined | { access_token: string; profile: Record<string, unknown> },
   search: '',
   signinRedirect: vi.fn(),
}));

vi.mock('react-i18next', () => ({
   useTranslation: () => ({
      t: (key: string, values?: Record<string, unknown>) => (values ? `${key} ${JSON.stringify(values)}` : key),
   }),
}));
vi.mock('react-oidc-context', () => ({
   useAuth: () => ({ user: state.user, isLoading: false, signinRedirect: state.signinRedirect }),
}));
vi.mock('react-router-dom', () => ({
   useLocation: () => ({ pathname: '/u/c1', search: state.search }),
}));
vi.mock('src/services/api/sessions', () => api);
vi.mock('../errors', () => ({ requestErrorMessage: (e: unknown) => String((e as Error).message ?? e) }));

import PassCard from './PassCard';

const page = (over: Record<string, unknown> = {}) =>
   ({
      creator: { creatorId: 'c1', displayName: 'Ada', passPriceMinor: 2000000, currency: 'NGN' },
      sessions: [],
      hasPass: false,
      ...over,
   } as any);

const settle = () => act(async () => {});
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
   state.user = undefined;
   state.search = '';
});
afterEach(() => {
   container.remove();
   document.body.innerHTML = '';
   vi.clearAllMocks();
});

test('a creator without a pass shows nothing', async () => {
   await act(async () =>
      createRoot(container).render(
         <PassCard page={page({ creator: { creatorId: 'c1', displayName: 'Ada' } })} onPaid={vi.fn()} />,
      ),
   );
   expect(document.querySelector('#pass-card')).toBeNull();
});

test('signed out: the buttons lead to the sign in and come back to this page', async () => {
   await act(async () => createRoot(container).render(<PassCard page={page()} onPaid={vi.fn()} />));

   expect(document.querySelector('#pass-card')?.textContent).toContain('₦20,000');
   await act(async () => (document.querySelector('#pass-sign-in') as HTMLElement).click());
   expect(state.signinRedirect).toHaveBeenCalledWith({ state: { url: '/u/c1' } });
   await act(async () => (document.querySelector('#pass-sign-up') as HTMLElement).click());
   expect(state.signinRedirect).toHaveBeenLastCalledWith({ state: { url: '/u/c1' }, prompt: 'create' });
});

test('signed in: paying needs a valid email, then goes to the page of the provider', async () => {
   state.user = { access_token: 'tok', profile: {} };
   api.startPassCheckout.mockResolvedValue({ authorizationUrl: 'about:blank#pay', alreadyHasTicket: false });
   const assign = vi.fn();
   Object.defineProperty(window, 'location', { value: { ...window.location, assign }, writable: true });
   await act(async () => createRoot(container).render(<PassCard page={page()} onPaid={vi.fn()} />));

   const pay = document.querySelector('#pass-pay') as HTMLButtonElement;
   expect(pay.disabled).toBe(true);
   await type('#pass-email', 'fan@example.com');
   expect(pay.disabled).toBe(false);
   await act(async () => pay.click());

   expect(api.startPassCheckout).toHaveBeenCalledWith('c1', 'fan@example.com', 'tok');
   expect(assign).toHaveBeenCalledWith('about:blank#pay');
});

test('a refused checkout shows the error of the server', async () => {
   state.user = { access_token: 'tok', profile: { email: 'fan@example.com' } };
   api.startPassCheckout.mockRejectedValue(new Error('This creator does not sell a pass.'));
   await act(async () => createRoot(container).render(<PassCard page={page()} onPaid={vi.fn()} />));

   await act(async () => (document.querySelector('#pass-pay') as HTMLElement).click());

   expect(document.querySelector('#pass-pay-error')?.textContent).toContain('does not sell a pass');
});

test('with a running pass the card says until when and offers to add time', async () => {
   state.user = { access_token: 'tok', profile: { email: 'fan@example.com' } };
   await act(async () =>
      createRoot(container).render(
         <PassCard page={page({ hasPass: true, passValidUntil: '2030-01-15T10:00:00Z' })} onPaid={vi.fn()} />,
      ),
   );

   expect(document.querySelector('#pass-active')?.textContent).toContain('creator.pass_active');
   expect(document.querySelector('#pass-pay')?.textContent).toContain('creator.pass_extend');
});

test('back from the payment page: when it is confirmed the page is loaded again', async () => {
   state.user = { access_token: 'tok', profile: { email: 'fan@example.com' } };
   state.search = '?reference=ref1&trxref=ref1';
   api.fetchPayment.mockResolvedValue({ status: 'paid', kind: 'pass' });
   const onPaid = vi.fn();
   await act(async () => createRoot(container).render(<PassCard page={page()} onPaid={onPaid} />));
   await settle();

   expect(api.fetchPayment).toHaveBeenCalledWith('ref1', 'tok');
   expect(onPaid).toHaveBeenCalled();
   expect(document.querySelector('#pass-paid')).not.toBeNull();
});

test('back from the payment page: a failed payment is said clearly', async () => {
   state.user = { access_token: 'tok', profile: { email: 'fan@example.com' } };
   state.search = '?reference=ref2';
   api.fetchPayment.mockResolvedValue({ status: 'failed', kind: 'pass' });
   await act(async () => createRoot(container).render(<PassCard page={page()} onPaid={vi.fn()} />));
   await settle();

   expect(document.querySelector('#pass-failed')).not.toBeNull();
});
