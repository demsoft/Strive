import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const auth = vi.hoisted(() => ({
   isLoading: false,
   isAuthenticated: false,
   activeNavigator: undefined as string | undefined,
   error: undefined as Error | undefined,
   signinRedirect: vi.fn(),
}));
const authParams = vi.hoisted(() => ({ present: false }));

vi.mock('react-oidc-context', () => ({ useAuth: () => auth, hasAuthParams: () => authParams.present }));
vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }) }));
vi.mock('src/components/BrandLogo', () => ({ default: () => null }));
vi.mock('src/RedirectToConference', () => ({ default: () => null }));
vi.mock('src/routes/AuthenticatedRoutes', () => ({ default: () => <div id="the-app" /> }));
vi.mock('src/features/auth/components/RequireAuth', () => ({
   default: ({ children }: { children: React.ReactNode }) => <div id="require-auth">{children}</div>,
}));

import LandingGate from './LandingGate';

let container: HTMLDivElement;
const render = () => act(async () => createRoot(container).render(<LandingGate />));

beforeEach(() => {
   auth.isLoading = false;
   auth.isAuthenticated = false;
   auth.activeNavigator = undefined;
   auth.error = undefined;
   auth.signinRedirect.mockReset();
   authParams.present = false;
   container = document.createElement('div');
   document.body.appendChild(container);
});
afterEach(() => {
   container.remove();
   document.body.innerHTML = '';
});

test('a visitor sees the welcome page with the sign up and sign in buttons', async () => {
   await render();

   expect(document.querySelector('#gbedu-landing')).not.toBeNull();
   expect(document.querySelector('#landing-sign-up')).not.toBeNull();
   expect(document.querySelector('#landing-sign-in')).not.toBeNull();
   expect(document.querySelector('#the-app')).toBeNull();
});

test('become a fan goes to the registration page and then to the home page, become a creator to the creator page', async () => {
   await render();

   await act(async () => (document.querySelector('#landing-sign-up') as HTMLElement).click());
   expect(auth.signinRedirect).toHaveBeenLastCalledWith({ state: { url: '/' }, prompt: 'create' });

   await act(async () => (document.querySelector('#landing-sign-up-creator') as HTMLElement).click());
   expect(auth.signinRedirect).toHaveBeenLastCalledWith({ state: { url: '/creator' }, prompt: 'create' });
});

test('an existing account signs in without the registration page', async () => {
   await render();

   await act(async () => (document.querySelector('#landing-sign-in') as HTMLElement).click());

   expect(auth.signinRedirect).toHaveBeenLastCalledWith({ state: { url: '/' } });
});

test('a signed in person gets the app, not the welcome page', async () => {
   auth.isAuthenticated = true;

   await render();

   expect(document.querySelector('#gbedu-landing')).toBeNull();
   expect(document.querySelector('#the-app')).not.toBeNull();
});

test('while the sign in state is not known the normal handling takes over, no flash of the welcome page', async () => {
   auth.isLoading = true;

   await render();

   expect(document.querySelector('#gbedu-landing')).toBeNull();
   expect(document.querySelector('#require-auth')).not.toBeNull();
});

test('when the sign in comes back with its answer the normal handling takes over', async () => {
   authParams.present = true;

   await render();

   expect(document.querySelector('#gbedu-landing')).toBeNull();
});
