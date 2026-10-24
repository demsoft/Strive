import React, { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const state = vi.hoisted(() => ({
   user: undefined as undefined | { access_token: string },
   path: '/creators',
   signinRedirect: vi.fn(),
}));

vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }) }));
vi.mock('react-oidc-context', () => ({ useAuth: () => ({ user: state.user, signinRedirect: state.signinRedirect }) }));
vi.mock('react-router-dom', () => ({
   Link: React.forwardRef<HTMLAnchorElement, { to: string; children: React.ReactNode; id?: string }>(
      ({ to, children, id }, ref) => (
         <a ref={ref} href={to} id={id}>
            {children}
         </a>
      ),
   ),
   useLocation: () => ({ pathname: state.path, search: '' }),
}));
vi.mock('src/components/BrandLogo', () => ({ default: () => null }));

import SiteHeader from './SiteHeader';

let container: HTMLDivElement;
let root: Root;
const render = () => act(async () => root.render(<SiteHeader />));

beforeEach(() => {
   container = document.createElement('div');
   document.body.appendChild(container);
   root = createRoot(container);
   state.user = undefined;
   state.path = '/creators';
});
afterEach(() => {
   act(() => root.unmount());
   container.remove();
   document.body.innerHTML = '';
   vi.clearAllMocks();
});

test('leads to the reels and the creators', async () => {
   await render();

   expect(document.querySelector('#nav-reels')?.getAttribute('href')).toBe('/reels');
   expect(document.querySelector('#nav-creators')?.getAttribute('href')).toBe('/creators');
});

test('a visitor can sign in from here and comes back to the same page', async () => {
   await render();

   await act(async () => document.querySelector<HTMLElement>('#nav-sign-in')?.click());

   expect(state.signinRedirect).toHaveBeenCalledWith({ state: { url: '/creators' } });
   expect(document.querySelector('#nav-home')).toBeNull();
});

test('somebody who is signed in gets a way home instead', async () => {
   state.user = { access_token: 'tok' };
   await render();

   expect(document.querySelector('#nav-home')?.getAttribute('href')).toBe('/');
   expect(document.querySelector('#nav-sign-in')).toBeNull();
});
