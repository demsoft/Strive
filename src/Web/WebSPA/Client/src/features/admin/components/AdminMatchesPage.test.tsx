import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({ fetchSessionReports: vi.fn(), keepSession: vi.fn(), takeDownSession: vi.fn() }));

vi.mock('react-i18next', () => ({
   useTranslation: () => ({
      t: (key: string, values?: Record<string, unknown>) => (values ? `${key} ${JSON.stringify(values)}` : key),
   }),
}));
vi.mock('react-router-dom', () => ({ Link: ({ children }: { children: React.ReactNode }) => <a>{children}</a> }));
vi.mock('src/components/BrandLogo', () => ({ default: () => null }));
vi.mock('../api', () => api);

import AdminMatchesPage from './AdminMatchesPage';

const item = (over: Record<string, unknown> = {}) => ({
   sessionId: 's1',
   title: 'Lagos FC vs Abuja United',
   creatorName: 'Lagos League',
   match: { homeTeam: 'Lagos FC', awayTeam: 'Abuja United', competition: 'State Cup' },
   rightsBasis: 'licensed',
   rightsNote: 'Lagos State FA, licence 12',
   priceMinor: 300000,
   tickets: 4,
   openReports: 2,
   reasons: { rightsholder: 1, illegal: 1 },
   details: [{ reason: 'rightsholder', text: 'This is our match', at: '2026-10-20T10:00:00Z' }],
   latestReportAt: '2026-10-20T10:00:00Z',
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
   await act(async () => root!.render(<AdminMatchesPage />));
   await settle();
};

test('a reported match shows what the organizer declared, the reports and the tickets', async () => {
   api.fetchSessionReports.mockResolvedValue([item()]);
   await render();

   const card = document.querySelector('.reported-session')!;
   expect(card.textContent).toContain('Lagos FC vs Abuja United');
   expect(card.textContent).toContain('matches.admin_rights_holder');
   expect(card.querySelector('.session-rights')?.textContent).toContain('Lagos State FA, licence 12');
   expect(card.textContent).toContain('This is our match');
   expect(card.textContent).toContain('"count":4');
});

test('nothing reported: says so', async () => {
   api.fetchSessionReports.mockResolvedValue([]);
   await render();

   expect(document.querySelector('#admin-matches-none')).not.toBeNull();
});

test('keeping a match puts the reports aside and asks again', async () => {
   api.fetchSessionReports.mockResolvedValue([item()]);
   api.keepSession.mockResolvedValue(undefined);
   await render();

   await act(async () => (document.querySelector('.session-keep') as HTMLButtonElement).click());
   await settle();

   expect(api.keepSession).toHaveBeenCalledWith('s1');
   expect(api.fetchSessionReports).toHaveBeenCalledTimes(2);
});

test('taking a match down asks for a note and sends it', async () => {
   api.fetchSessionReports.mockResolvedValue([item()]);
   api.takeDownSession.mockResolvedValue(undefined);
   await render();

   await act(async () => (document.querySelector('.session-takedown') as HTMLButtonElement).click());
   const note = document.querySelector('#takedown-note') as HTMLTextAreaElement;
   const setter = Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value')!.set!;
   await act(async () => {
      setter.call(note, '  Not your match  ');
      note.dispatchEvent(new Event('input', { bubbles: true }));
   });
   await act(async () => (document.querySelector('#takedown-confirm') as HTMLButtonElement).click());
   await settle();

   expect(api.takeDownSession).toHaveBeenCalledWith('s1', 'Not your match');
});

test('a person who is not an administrator is told so', async () => {
   api.fetchSessionReports.mockRejectedValue({ response: { status: 403 } });
   await render();

   expect(document.querySelector('#admin-matches-forbidden')).not.toBeNull();
});
