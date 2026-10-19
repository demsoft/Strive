import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }) }));
vi.mock('react-router-dom', () => ({
   Link: React.forwardRef<HTMLAnchorElement, { to: string; children: React.ReactNode }>(
      ({ to, children, ...rest }, ref) => (
         <a ref={ref} href={to} {...rest}>
            {children}
         </a>
      ),
   ),
}));

import GbeduRoles from './GbeduRoles';

let container: HTMLDivElement;
beforeEach(() => {
   container = document.createElement('div');
   document.body.appendChild(container);
});
afterEach(() => {
   container.remove();
   document.body.innerHTML = '';
});

test('the two roles are shown: a fan goes to the tickets, a creator to the creator page', async () => {
   await act(async () => createRoot(container).render(<GbeduRoles />));

   expect(document.querySelector('#roles-fan')?.getAttribute('href')).toBe('/tickets');
   expect(document.querySelector('#roles-creator')?.getAttribute('href')).toBe('/creator');
});
