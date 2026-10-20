import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({
   fetchMySessions: vi.fn(),
   createSession: vi.fn(),
   cancelSession: vi.fn(),
   fetchMyTickets: vi.fn(),
   fetchMyPasses: vi.fn(),
   sessionLink: (id: string) => `https://gbedu.test/s/${id}`,
}));
const push = vi.hoisted(() => vi.fn());

vi.mock('react-i18next', () => ({
   useTranslation: () => ({
      t: (key: string, values?: Record<string, unknown>) => (values ? `${key} ${JSON.stringify(values)}` : key),
   }),
}));
vi.mock('react-router-dom', () => ({
   Link: ({ children, to, className }: { children: React.ReactNode; to?: string; className?: string }) => (
      <a href={to} className={className}>
         {children}
      </a>
   ),
   useHistory: () => ({ push }),
}));
vi.mock('src/components/BrandLogo', () => ({ default: () => null }));
vi.mock('src/services/api/sessions', () => api);
vi.mock('src/features/creator/errors', () => ({
   requestErrorMessage: (e: unknown) => String((e as Error).message ?? e),
}));

import MySessionsPage from './MySessionsPage';
import MyTicketsPage from './MyTicketsPage';

let container: HTMLDivElement;
const settle = () => act(async () => new Promise((resolve) => setTimeout(resolve, 0)));

const typeInto = async (selector: string, value: string) => {
   const input = document.querySelector(selector) as HTMLInputElement | HTMLTextAreaElement;
   const proto = input instanceof HTMLTextAreaElement ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
   const setter = Object.getOwnPropertyDescriptor(proto, 'value')!.set!;
   await act(async () => {
      setter.call(input, value);
      input.dispatchEvent(new Event('input', { bubbles: true }));
   });
};

beforeEach(() => {
   Object.values(api).forEach(
      (fn) => typeof fn === 'function' && 'mockReset' in fn && (fn as ReturnType<typeof vi.fn>).mockReset(),
   );
   api.fetchMyPasses.mockResolvedValue([]);
   push.mockReset();
   container = document.createElement('div');
   document.body.appendChild(container);
});
afterEach(() => {
   container.remove();
   document.body.innerHTML = '';
});

test('publishing needs a title and a price in range, and sends the price in kobo', async () => {
   api.fetchMySessions.mockResolvedValue([]);
   api.createSession.mockImplementation(async (r) => ({
      sessionId: 'abcd2345',
      conferenceId: 'room1',
      currency: 'NGN',
      status: 'published',
      createdAt: '',
      ...r,
   }));
   await act(async () => createRoot(container).render(<MySessionsPage />));
   await settle();
   const publish = () => document.querySelector('#session-publish') as HTMLButtonElement;
   expect(publish().disabled).toBe(true);

   await typeInto('#session-form-title', 'Piano for beginners');
   await typeInto('#session-form-price', '50');
   expect(publish().disabled).toBe(true); // below ₦100

   await typeInto('#session-form-price', '5,000');
   expect(publish().disabled).toBe(false);
   await act(async () => publish().click());
   await settle();

   expect(api.createSession).toHaveBeenCalledWith(
      expect.objectContaining({ title: 'Piano for beginners', category: 'teach', priceMinor: 500000 }),
   );
   expect(document.querySelector('#session-created')).not.toBeNull();
});

test('the server error of a refused session is shown', async () => {
   api.fetchMySessions.mockResolvedValue([]);
   api.createSession.mockRejectedValue(new Error('Add and verify your bank account before you sell tickets.'));
   await act(async () => createRoot(container).render(<MySessionsPage />));
   await settle();
   await typeInto('#session-form-title', 'Piano for beginners');
   await typeInto('#session-form-price', '5000');

   await act(async () => (document.querySelector('#session-publish') as HTMLElement).click());
   await settle();

   expect(document.querySelector('#session-form-error')?.textContent).toContain('bank account');
});

test('published sessions are listed with their price and a way into the room', async () => {
   api.fetchMySessions.mockResolvedValue([
      {
         sessionId: 'abcd2345',
         conferenceId: 'room1',
         title: 'Piano',
         category: 'music',
         priceMinor: 500000,
         currency: 'NGN',
         status: 'published',
         createdAt: '',
      },
      {
         sessionId: 'zzzz2345',
         conferenceId: 'room2',
         title: 'Old class',
         category: 'teach',
         priceMinor: 100000,
         currency: 'NGN',
         status: 'cancelled',
         createdAt: '',
         refunded: 3,
         refundsPending: 0,
      },
   ]);
   await act(async () => createRoot(container).render(<MySessionsPage />));
   await settle();

   const rows = document.querySelectorAll('.my-session');
   expect(rows).toHaveLength(2);
   expect(rows[0].textContent).toContain('₦5,000');
   expect(rows[1].textContent).toContain('sessions.cancelled_chip');
   // everything was given back: said so, nothing to retry
   expect(rows[1].querySelector('.refund-summary')?.textContent).toContain('sessions.refunds_done');
   expect(rows[1].querySelector('.retry-refunds')).toBeNull();
});

test('a refund that did not go through can be tried again', async () => {
   api.fetchMySessions.mockResolvedValue([
      {
         sessionId: 'zzzz2345',
         conferenceId: 'room2',
         title: 'Old class',
         category: 'teach',
         priceMinor: 100000,
         currency: 'NGN',
         status: 'cancelled',
         createdAt: '',
         refunded: 1,
         refundsPending: 2,
      },
   ]);
   api.cancelSession.mockResolvedValue(undefined);
   await act(async () => createRoot(container).render(<MySessionsPage />));
   await settle();

   expect(document.querySelector('.refund-summary')?.textContent).toContain('sessions.refunds_open');
   await act(async () => (document.querySelector('.retry-refunds') as HTMLElement).click());
   expect(api.cancelSession).toHaveBeenCalledWith('zzzz2345');
});

test('my tickets lists the sessions with a join button, a called off one is under past and has none', async () => {
   api.fetchMyTickets.mockResolvedValue([
      {
         sessionId: 'a',
         conferenceId: 'room1',
         title: 'Piano',
         category: 'music',
         creatorName: 'Ada',
         grantedAt: '',
         status: 'published',
      },
      {
         sessionId: 'b',
         conferenceId: 'room2',
         title: 'Old',
         category: 'teach',
         creatorName: 'Kofi',
         grantedAt: '',
         status: 'cancelled',
         refunded: true,
      },
   ]);
   await act(async () => createRoot(container).render(<MyTicketsPage />));
   await settle();

   const upcoming = document.querySelectorAll('.my-ticket');
   expect(upcoming).toHaveLength(1);
   await act(async () => (upcoming[0].querySelector('button') as HTMLElement).click());
   expect(push).toHaveBeenCalledWith('/c/room1');

   await act(async () => (document.querySelector('#tickets-tab-past') as HTMLElement).click());
   const past = document.querySelectorAll('.my-ticket');
   expect(past).toHaveLength(1);
   expect(past[0].textContent).toContain('Old');
   expect(past[0].querySelector('button')).toBeNull();
   expect(past[0].querySelector('.ticket-refunded')).not.toBeNull();
});

test('without tickets there is a friendly empty state', async () => {
   api.fetchMyTickets.mockResolvedValue([]);
   await act(async () => createRoot(container).render(<MyTicketsPage />));
   await settle();

   expect(document.querySelector('#no-tickets')).not.toBeNull();
   expect(document.querySelectorAll('.my-ticket')).toHaveLength(0);
});

test('my tickets shows the running passes, each leading to the page of the creator', async () => {
   api.fetchMyTickets.mockResolvedValue([]);
   api.fetchMyPasses.mockResolvedValue([{ creatorId: 'c1', creatorName: 'Ada', validUntil: '2030-01-15T10:00:00Z' }]);
   await act(async () => createRoot(container).render(<MyTicketsPage />));
   await settle();

   const passes = document.querySelectorAll('.my-pass');
   expect(passes).toHaveLength(1);
   expect(passes[0].textContent).toContain('Ada');
   expect(passes[0].getAttribute('href')).toBe('/u/c1');
});

test('my tickets still works if the passes cannot be loaded', async () => {
   api.fetchMyTickets.mockResolvedValue([]);
   api.fetchMyPasses.mockRejectedValue(new Error('down'));
   await act(async () => createRoot(container).render(<MyTicketsPage />));
   await settle();

   expect(document.querySelector('#no-tickets')).not.toBeNull();
   expect(document.querySelector('#my-passes')).toBeNull();
});
