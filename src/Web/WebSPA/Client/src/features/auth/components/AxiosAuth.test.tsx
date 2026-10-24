import Axios from 'axios';
import React, { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const state = vi.hoisted(() => ({
   user: undefined as undefined | { access_token: string },
   isLoading: false,
}));
vi.mock('react-oidc-context', () => ({ useAuth: () => ({ user: state.user, isLoading: state.isLoading }) }));

import AxiosAuth from './AxiosAuth';

let container: HTMLDivElement;
let root: Root;
const render = () => act(async () => root.render(<AxiosAuth />));
const header = () => (Axios.defaults.headers.common as Record<string, unknown>).Authorization;

beforeEach(() => {
   container = document.createElement('div');
   document.body.appendChild(container);
   root = createRoot(container);
   state.user = undefined;
   state.isLoading = false;
   delete (Axios.defaults.headers.common as Record<string, unknown>).Authorization;
});
afterEach(() => {
   act(() => root.unmount());
   container.remove();
   delete (Axios.defaults.headers.common as Record<string, unknown>).Authorization;
});

test('a signed in person calls the API with their access token', async () => {
   state.user = { access_token: 'tok-1' };
   await render();

   expect(header()).toBe('Bearer tok-1');
});

test('a renewed token replaces the old one', async () => {
   state.user = { access_token: 'tok-1' };
   await render();
   state.user = { access_token: 'tok-2' };
   await render();

   expect(header()).toBe('Bearer tok-2');
});

test('a visitor sends none, and what was there from before is taken away', async () => {
   Axios.defaults.headers.common = { ...Axios.defaults.headers.common, Authorization: 'Bearer old' };
   await render();

   expect(header()).toBeUndefined();
});

test('while it is not known yet whether somebody is signed in nothing is changed', async () => {
   Axios.defaults.headers.common = { ...Axios.defaults.headers.common, Authorization: 'Bearer kept' };
   state.isLoading = true;
   await render();

   expect(header()).toBe('Bearer kept');
});

test('other headers stay', async () => {
   Axios.defaults.headers.common = { ...Axios.defaults.headers.common, 'X-Test': '1', Authorization: 'Bearer old' };
   await render();

   expect((Axios.defaults.headers.common as Record<string, unknown>)['X-Test']).toBe('1');
});
