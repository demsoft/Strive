import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({ createSession: vi.fn() }));

vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }) }));
vi.mock('src/services/api/sessions', () => api);
vi.mock('src/features/creator/errors', () => ({
   requestErrorMessage: (e: unknown) => String((e as Error).message ?? e),
}));

import NewMatchForm from './NewMatchForm';

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

const render = async (onCreated = vi.fn()) => {
   root = createRoot(container);
   await act(async () => root!.render(<NewMatchForm onCreated={onCreated} />));
   await settle();
   return onCreated;
};

const type = async (id: string, value: string) => {
   const input = document.querySelector(id) as HTMLInputElement | HTMLTextAreaElement;
   const proto = input instanceof HTMLTextAreaElement ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
   const setter = Object.getOwnPropertyDescriptor(proto, 'value')!.set!;
   await act(async () => {
      setter.call(input, value);
      input.dispatchEvent(new Event('input', { bubbles: true }));
   });
};
const accept = () => act(async () => (document.querySelector('#match-rights-accept') as HTMLInputElement).click());
const publish = () => document.querySelector('#match-publish') as HTMLButtonElement;

const fillTeamsAndPrice = async () => {
   await type('#match-home', 'Lagos FC');
   await type('#match-away', 'Abuja United');
   await type('#match-price', '3000');
};

test('nothing can be published without the teams, a price and the declaration', async () => {
   await render();
   expect(publish().disabled).toBe(true);

   await fillTeamsAndPrice();
   expect(publish().disabled).toBe(true);

   await accept();
   expect(publish().disabled).toBe(false);
});

test('the match and the declaration are sent as a match, the title is left to the server', async () => {
   api.createSession.mockResolvedValue({ sessionId: 's1', title: 'Lagos FC vs Abuja United' });
   const onCreated = await render();
   await fillTeamsAndPrice();
   await type('#match-competition', '  State Cup ');
   await accept();

   await act(async () => publish().click());
   await settle();

   expect(api.createSession).toHaveBeenCalledWith(
      expect.objectContaining({
         title: '',
         category: 'match',
         priceMinor: 300000,
         match: {
            homeTeam: 'Lagos FC',
            awayTeam: 'Abuja United',
            competition: 'State Cup',
            rightsBasis: 'own',
            rightsNote: undefined,
            rightsAccepted: true,
         },
      }),
   );
   expect(onCreated).toHaveBeenCalledWith({ sessionId: 's1', title: 'Lagos FC vs Abuja United' });
   // the form is empty again
   expect((document.querySelector('#match-home') as HTMLInputElement).value).toBe('');
   expect(publish().disabled).toBe(true);
});

test('a refusal of the server is shown and the form keeps what was typed', async () => {
   api.createSession.mockRejectedValue(new Error('Say why you may stream this match.'));
   const onCreated = await render();
   await fillTeamsAndPrice();
   await accept();

   await act(async () => publish().click());
   await settle();

   expect(document.querySelector('#match-form-error')?.textContent).toContain('Say why');
   expect((document.querySelector('#match-home') as HTMLInputElement).value).toBe('Lagos FC');
   expect(onCreated).not.toHaveBeenCalled();
});
