import React, { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const history = vi.hoisted(() => ({ go: vi.fn(), push: vi.fn() }));
vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }) }));
vi.mock('react-router-dom', () => ({ useHistory: () => history }));

import BackButton from './BackButton';

let container: HTMLDivElement;
let root: Root;

beforeEach(() => {
   container = document.createElement('div');
   document.body.appendChild(container);
   root = createRoot(container);
});
afterEach(() => {
   act(() => root.unmount());
   container.remove();
   document.body.innerHTML = '';
   vi.restoreAllMocks();
   vi.clearAllMocks();
});

const press = async () => {
   await act(async () => root.render(<BackButton fallback="/creator" />));
   await act(async () => document.querySelector<HTMLElement>('#back-button')?.click());
};

test('goes back to where the person came from', async () => {
   vi.spyOn(window.history, 'length', 'get').mockReturnValue(5);

   await press();

   expect(history.go).toHaveBeenCalledWith(-1);
   expect(history.push).not.toHaveBeenCalled();
});

test('with nothing to go back to (a page opened by its address) it goes to the fallback', async () => {
   vi.spyOn(window.history, 'length', 'get').mockReturnValue(1);

   await press();

   expect(history.push).toHaveBeenCalledWith('/creator');
   expect(history.go).not.toHaveBeenCalled();
});
