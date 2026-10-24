import React, { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }) }));
vi.mock('react-router-dom', () => ({
   Link: React.forwardRef<HTMLAnchorElement, { to: string; children: React.ReactNode; id?: string }>(
      ({ to, children, id }, ref) => (
         <a ref={ref} href={to} id={id}>
            {children}
         </a>
      ),
   ),
}));
vi.mock('src/services/api/sessions', () => ({ creatorPageLink: (path: string) => `https://gbedu.test/u/${path}` }));

import ProfileHero from './ProfileHero';

const profile = (over: Record<string, unknown> = {}) =>
   ({
      creatorId: 'c1',
      displayName: 'Ada Lovelace',
      bio: 'Piano',
      country: 'NG',
      currency: 'NGN',
      canSell: true,
      createdAt: '2026-10-01T10:00:00Z',
      ...over,
   } as never);

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
   vi.clearAllMocks();
});

test('shows the name, the address of the page without https, and the way to the page when the creator can sell', async () => {
   await act(async () => root.render(<ProfileHero profile={profile({ slug: 'ada' })} />));

   expect(document.querySelector('#creator-hero-name')?.textContent).toBe('Ada Lovelace');
   expect(document.querySelector('.creator-link-text')?.textContent).toBe('gbedu.test/u/ada');
   expect(document.querySelector('#creator-page-link')?.getAttribute('href')).toBe('/u/ada');
   expect(document.querySelector('#creator-status')?.textContent).toContain('dashboard.status_ready');
});

test('without a verified bank account it says what is missing and has no button to the page', async () => {
   await act(async () => root.render(<ProfileHero profile={profile({ canSell: false })} />));

   expect(document.querySelector('#creator-status')?.textContent).toContain('dashboard.status_setup');
   expect(document.querySelector('#creator-hero-setup-hint')).not.toBeNull();
   expect(document.querySelector('#creator-page-link')).toBeNull();
   // the address is the id, until a short name is chosen
   expect(document.querySelector('.creator-link-text')?.textContent).toBe('gbedu.test/u/c1');
});

test('the address is copied with one click', async () => {
   const writeText = vi.fn(() => Promise.resolve());
   Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
   await act(async () => root.render(<ProfileHero profile={profile({ slug: 'ada' })} />));

   await act(async () => document.querySelector<HTMLElement>('#creator-copy-link')?.click());

   expect(writeText).toHaveBeenCalledWith('https://gbedu.test/u/ada');
});
