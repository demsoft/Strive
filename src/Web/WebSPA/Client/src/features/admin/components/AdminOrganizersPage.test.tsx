import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({ fetchOrganizers: vi.fn(), decideOrganizer: vi.fn() }));

vi.mock('react-i18next', () => ({
   useTranslation: () => ({
      t: (key: string, values?: Record<string, unknown>) => (values ? `${key} ${JSON.stringify(values)}` : key),
   }),
}));
vi.mock('react-router-dom', () => ({ Link: ({ children }: { children: React.ReactNode }) => <a>{children}</a> }));
vi.mock('src/components/BrandLogo', () => ({ default: () => null }));
vi.mock('../api', () => api);

import AdminOrganizersPage from './AdminOrganizersPage';

const item = (over: Record<string, unknown> = {}) => ({
   creatorId: 'c1',
   displayName: 'Ada',
   organizationName: 'Abuja Schools League',
   about: 'Inter-school football, run by the ministry',
   status: 'requested',
   requestedAt: '2026-10-20T10:00:00Z',
   matches: 0,
   ...over,
});

const settle = () => act(async () => {});
let container: HTMLDivElement;
let root: ReturnType<typeof createRoot> | null = null;

beforeEach(() => {
   container = document.createElement('div');
   document.body.appendChild(container);
});
afterEach(async () => {
   await act(async () => root?.unmount());
   root = null;
   container.remove();
   document.body.innerHTML = '';
   vi.clearAllMocks();
});

const render = async () => {
   root = createRoot(container);
   await act(async () => root!.render(<AdminOrganizersPage />));
   await settle();
};
const click = (selector: string) => act(async () => (document.querySelector(selector) as HTMLElement).click());

test('a request shows who asks and what for, with both decisions', async () => {
   api.fetchOrganizers.mockResolvedValue([item()]);
   await render();

   const card = document.querySelector('.organizer')!;
   expect(card.textContent).toContain('Abuja Schools League');
   expect(card.querySelector('.organizer-about')?.textContent).toContain('run by the ministry');
   expect(card.querySelector('.organizer-approve')).not.toBeNull();
   expect(card.querySelector('.organizer-reject')).not.toBeNull();
});

test('approving asks for an optional note and sends the decision', async () => {
   api.fetchOrganizers.mockResolvedValue([item()]);
   api.decideOrganizer.mockResolvedValue(undefined);
   await render();

   await click('.organizer-approve');
   const note = document.querySelector('#organizer-note') as HTMLTextAreaElement;
   await act(async () => {
      Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value')!.set!.call(note, ' Known league ');
      note.dispatchEvent(new Event('input', { bubbles: true }));
   });
   await click('#organizer-confirm');
   await settle();

   expect(api.decideOrganizer).toHaveBeenCalledWith('c1', true, 'Known league');
   expect(api.fetchOrganizers).toHaveBeenCalledTimes(2);
});

test('an approved organizer can be taken back, not approved again', async () => {
   api.fetchOrganizers.mockResolvedValue([item({ status: 'approved', matches: 3 })]);
   api.decideOrganizer.mockResolvedValue(undefined);
   await render();

   expect(document.querySelector('.organizer-approve')).toBeNull();
   await click('.organizer-reject');
   await click('#organizer-confirm');
   await settle();

   expect(api.decideOrganizer).toHaveBeenCalledWith('c1', false, '');
});

test('nobody asked: says so; not an administrator: told so', async () => {
   api.fetchOrganizers.mockResolvedValue([]);
   await render();
   expect(document.querySelector('#admin-organizers-none')).not.toBeNull();

   await act(async () => root!.unmount());
   root = null;
   api.fetchOrganizers.mockRejectedValue({ response: { status: 403 } });
   await render();
   expect(document.querySelector('#admin-organizers-forbidden')).not.toBeNull();
});
