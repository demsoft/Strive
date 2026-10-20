import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({
   fetchCreatorPage: vi.fn(),
   creatorPageLink: (id: string) => `https://gbedu.test/u/${id}`,
}));

vi.mock('react-i18next', () => ({
   useTranslation: () => ({
      t: (key: string, values?: Record<string, unknown>) => (values ? `${key} ${JSON.stringify(values)}` : key),
   }),
}));
vi.mock('react-oidc-context', () => ({ useAuth: () => ({ user: undefined, isLoading: false }) }));
vi.mock('react-router-dom', () => ({
   Link: React.forwardRef<HTMLAnchorElement, { to: string; children: React.ReactNode }>(
      ({ to, children, ...rest }: any, ref) => (
         <a ref={ref} href={to} className={rest.className}>
            {children}
         </a>
      ),
   ),
   useParams: () => ({ creatorId: 'c1' }),
}));
vi.mock('src/components/BrandLogo', () => ({ default: () => null }));
vi.mock('src/services/api/sessions', () => api);

import CreatorPublicPage from './CreatorPublicPage';

const settle = () => act(async () => {});
const session = (id: string) => ({
   sessionId: id,
   title: 'Piano ' + id,
   category: 'music',
   priceMinor: 500000,
   currency: 'NGN',
   startsAt: null,
   creatorName: 'Ada',
   isHost: false,
   hasTicket: false,
});

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

test('shows the creator and links every session to its page', async () => {
   api.fetchCreatorPage.mockResolvedValue({
      creator: { creatorId: 'c1', displayName: 'Ada Lovelace', bio: 'Piano teacher' },
      sessions: [session('aaaa1111'), session('bbbb2222')],
   });
   await act(async () => createRoot(container).render(<CreatorPublicPage />));
   await settle();

   expect(document.querySelector('#creator-name')?.textContent).toBe('Ada Lovelace');
   expect(document.querySelector('#creator-bio')?.textContent).toBe('Piano teacher');
   expect([...document.querySelectorAll('.discover-session')].map((x) => x.getAttribute('href'))).toEqual([
      '/s/aaaa1111',
      '/s/bbbb2222',
   ]);
   expect(api.fetchCreatorPage).toHaveBeenCalledWith('c1', undefined);
});

test('a creator with nothing on says so', async () => {
   api.fetchCreatorPage.mockResolvedValue({ creator: { creatorId: 'c1', displayName: 'Ada' }, sessions: [] });
   await act(async () => createRoot(container).render(<CreatorPublicPage />));
   await settle();

   expect(document.querySelector('#creator-nothing-on')).not.toBeNull();
});

test('an unknown creator shows the not found message', async () => {
   api.fetchCreatorPage.mockRejectedValue(new Error('404'));
   await act(async () => createRoot(container).render(<CreatorPublicPage />));
   await settle();

   expect(document.querySelector('#creator-public-page')?.textContent).toContain('creator.page_not_found');
});

test('the share button copies the link of the page', async () => {
   api.fetchCreatorPage.mockResolvedValue({ creator: { creatorId: 'c1', displayName: 'Ada' }, sessions: [] });
   const writeText = vi.fn().mockResolvedValue(undefined);
   Object.assign(navigator, { clipboard: { writeText } });
   await act(async () => createRoot(container).render(<CreatorPublicPage />));
   await settle();

   await act(async () => (document.querySelector('#creator-share') as HTMLElement).click());

   expect(writeText).toHaveBeenCalledWith('https://gbedu.test/u/c1');
});
