import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({
   fetchSession: vi.fn(),
   startCheckout: vi.fn(),
   fetchPayment: vi.fn(),
}));
const push = vi.hoisted(() => vi.fn());
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
   Link: ({ children }: { children: React.ReactNode }) => <a>{children}</a>,
   useHistory: () => ({ push, go: vi.fn() }),
   useLocation: () => ({ pathname: '/s/abcd2345', search: state.search }),
   useParams: () => ({ sessionId: 'abcd2345' }),
}));
vi.mock('src/components/BrandLogo', () => ({ default: () => null }));
vi.mock('src/services/api/sessions', () => api);
vi.mock('src/features/creator/errors', () => ({
   requestErrorMessage: (e: unknown) => String((e as Error).message ?? e),
}));

import SessionPage from './SessionPage';

const baseSession = {
   sessionId: 'abcd2345',
   title: 'Piano for beginners',
   description: 'Learn the basics',
   category: 'music',
   priceMinor: 500000,
   currency: 'NGN',
   status: 'published',
   startsAt: null,
   creator: { creatorId: 'c1', displayName: 'Ada Lovelace', bio: 'Piano teacher' },
   isHost: false,
   hasTicket: false,
   conferenceId: null,
};

let container: HTMLDivElement;
const settle = () => act(async () => new Promise((resolve) => setTimeout(resolve, 0)));

async function render() {
   await act(async () => createRoot(container).render(<SessionPage />));
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

const assign = vi.fn();

beforeEach(() => {
   Object.values(api).forEach((fn) => fn.mockReset());
   push.mockReset();
   assign.mockReset();
   state.user = undefined;
   state.search = '';
   state.signinRedirect.mockReset();
   api.fetchSession.mockResolvedValue({ ...baseSession });
   Object.defineProperty(window, 'location', { value: { ...window.location, assign }, configurable: true });
   container = document.createElement('div');
   document.body.appendChild(container);
});
afterEach(() => {
   container.remove();
   document.body.innerHTML = '';
});

test('a visitor reads the offer and is asked to sign in to buy, without seeing a payment form', async () => {
   await render();

   expect(document.querySelector('#session-title')?.textContent).toBe('Piano for beginners');
   expect(document.querySelector('#session-sign-in')?.textContent).toContain('₦5,000');
   expect(document.querySelector('#session-buy')).toBeNull();
   expect(api.fetchSession).toHaveBeenCalledWith('abcd2345', undefined);

   await act(async () => (document.querySelector('#session-sign-in') as HTMLElement).click());
   expect(state.signinRedirect).toHaveBeenCalledWith({ state: { url: '/s/abcd2345' } });
});

test('a session that does not exist says so', async () => {
   api.fetchSession.mockRejectedValue({ response: { status: 404 } });

   await render();

   expect(document.querySelector('#session-not-found')).not.toBeNull();
});

test('a signed in fan enters an email and is sent to the payment page', async () => {
   state.user = { access_token: 'tok', profile: {} };
   api.startCheckout.mockResolvedValue({
      authorizationUrl: 'https://checkout.paystack.com/abc',
      reference: 'gbd_1',
      alreadyHasTicket: false,
   });
   await render();
   expect((document.querySelector('#session-pay') as HTMLButtonElement).disabled).toBe(true);

   await typeInto('#session-email', 'fan@example.com');
   await act(async () => (document.querySelector('#session-pay') as HTMLElement).click());
   await settle();

   expect(api.startCheckout).toHaveBeenCalledWith('abcd2345', 'fan@example.com', 'tok');
   expect(assign).toHaveBeenCalledWith('https://checkout.paystack.com/abc');
});

test('an error of the payment service is shown and the fan can try again', async () => {
   state.user = { access_token: 'tok', profile: { email: 'fan@example.com' } };
   api.startCheckout.mockRejectedValue(new Error('The payment service did not answer.'));
   await render();

   await act(async () => (document.querySelector('#session-pay') as HTMLElement).click());
   await settle();

   expect(document.querySelector('#session-pay-error')?.textContent).toContain('did not answer');
   expect(assign).not.toHaveBeenCalled();
   expect((document.querySelector('#session-pay') as HTMLButtonElement).disabled).toBe(false);
});

test('a fan with a ticket gets the way into the room, not a payment form', async () => {
   state.user = { access_token: 'tok', profile: {} };
   api.fetchSession.mockResolvedValue({ ...baseSession, hasTicket: true, conferenceId: 'room1' });
   await render();

   expect(document.querySelector('#session-buy')).toBeNull();
   await act(async () => (document.querySelector('#session-join') as HTMLElement).click());
   expect(push).toHaveBeenCalledWith('/c/room1');
});

test('the host opens the room', async () => {
   state.user = { access_token: 'tok', profile: {} };
   api.fetchSession.mockResolvedValue({ ...baseSession, isHost: true, conferenceId: 'room1' });
   await render();

   await act(async () => (document.querySelector('#session-open-room') as HTMLElement).click());

   expect(push).toHaveBeenCalledWith('/c/room1');
});

test('a called off session cannot be bought', async () => {
   state.user = { access_token: 'tok', profile: {} };
   api.fetchSession.mockResolvedValue({ ...baseSession, status: 'cancelled' });
   await render();

   expect(document.querySelector('#session-cancelled')).not.toBeNull();
   expect(document.querySelector('#session-pay')).toBeNull();
});

test('coming back from the payment page the page waits for the confirmation and then shows the ticket', async () => {
   state.user = { access_token: 'tok', profile: {} };
   state.search = '?reference=gbd_1';
   api.fetchPayment.mockResolvedValue({
      reference: 'gbd_1',
      status: 'paid',
      sessionId: 'abcd2345',
      hasTicket: true,
      conferenceId: 'room1',
   });
   api.fetchSession
      .mockResolvedValueOnce({ ...baseSession })
      .mockResolvedValue({ ...baseSession, hasTicket: true, conferenceId: 'room1' });

   await render();
   await settle();

   expect(api.fetchPayment).toHaveBeenCalledWith('gbd_1', 'tok');
   expect(document.querySelector('#session-has-ticket')).not.toBeNull();
});

test('a payment that failed says so and offers to pay again', async () => {
   state.user = { access_token: 'tok', profile: {} };
   state.search = '?reference=gbd_1';
   api.fetchPayment.mockResolvedValue({
      reference: 'gbd_1',
      status: 'failed',
      sessionId: 'abcd2345',
      hasTicket: false,
   });

   await render();
   await settle();

   expect(document.querySelector('#payment-failed')).not.toBeNull();
   expect(document.querySelector('#session-pay')).not.toBeNull();
});
