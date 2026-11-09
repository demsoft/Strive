import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({ fetchMyCreatorProfile: vi.fn(), requestOrganizer: vi.fn() }));

vi.mock('react-i18next', () => ({
   useTranslation: () => ({
      t: (key: string, values?: Record<string, unknown>) => (values ? `${key} ${JSON.stringify(values)}` : key),
   }),
}));
vi.mock('src/services/api/creators', () => api);
vi.mock('src/features/creator/errors', () => ({
   requestErrorMessage: (e: unknown) => String((e as Error).message ?? e),
}));
vi.mock('./NewMatchForm', () => ({ default: () => <div id="new-match-form" /> }));

import MatchSetup from './MatchSetup';

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
   await act(async () => root!.render(<MatchSetup onCreated={vi.fn()} />));
   await settle();
};

const type = async (id: string, value: string) => {
   const input = document.querySelector(id) as HTMLInputElement | HTMLTextAreaElement;
   const proto = input instanceof HTMLTextAreaElement ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
   await act(async () => {
      Object.getOwnPropertyDescriptor(proto, 'value')!.set!.call(input, value);
      input.dispatchEvent(new Event('input', { bubbles: true }));
   });
};

test('an approved organizer gets the form', async () => {
   api.fetchMyCreatorProfile.mockResolvedValue({ organizerStatus: 'approved' });
   await render();

   expect(document.querySelector('#new-match-form')).not.toBeNull();
   expect(document.querySelector('#organizer-request')).toBeNull();
});

test('someone who never asked sees the request, and sends it', async () => {
   api.fetchMyCreatorProfile.mockResolvedValue({ organizerStatus: 'none', organizationName: null });
   api.requestOrganizer.mockResolvedValue({ organizerStatus: 'requested', organizationName: 'Lagos League' });
   await render();
   expect(document.querySelector('#new-match-form')).toBeNull();
   expect((document.querySelector('#organizer-send') as HTMLButtonElement).disabled).toBe(true);

   await type('#organizer-name', ' Lagos League ');
   await type('#organizer-about', ' Our league, listed by the federation ');
   await act(async () => (document.querySelector('#organizer-send') as HTMLButtonElement).click());
   await settle();

   expect(api.requestOrganizer).toHaveBeenCalledWith('Lagos League', 'Our league, listed by the federation');
   expect(document.querySelector('#organizer-requested')?.textContent).toContain('Lagos League');
});

test('a request that is waiting is shown as waiting', async () => {
   api.fetchMyCreatorProfile.mockResolvedValue({ organizerStatus: 'requested', organizationName: 'Abuja Schools' });
   await render();

   expect(document.querySelector('#organizer-requested')).not.toBeNull();
   expect(document.querySelector('#organizer-send')).toBeNull();
});

test('a request that was turned down says why and can be sent again', async () => {
   api.fetchMyCreatorProfile.mockResolvedValue({
      organizerStatus: 'rejected',
      organizationName: 'Abuja Schools',
      organizerDecisionNote: 'We could not verify the league',
   });
   await render();

   expect(document.querySelector('#organizer-rejected')?.textContent).toContain('We could not verify the league');
   expect(document.querySelector('#organizer-send')).not.toBeNull();
});

test('a person without a creator profile is sent to make one', async () => {
   api.fetchMyCreatorProfile.mockResolvedValue(null);
   await render();

   expect(document.querySelector('#match-setup-no-profile')).not.toBeNull();
});
