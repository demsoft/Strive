import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({ setSlug: vi.fn() }));

vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }) }));
vi.mock('src/services/api/creators', () => api);
vi.mock('../errors', () => ({ requestErrorMessage: (e: unknown) => String((e as Error).message ?? e) }));

import SlugSetup, { isPlausibleSlug } from './SlugSetup';

const profile = (over: Record<string, unknown> = {}) =>
   ({ creatorId: 'c'.repeat(32), displayName: 'Ada', currency: 'NGN', canSell: true, ...over } as any);

const type = async (value: string) => {
   const input = document.querySelector('#slug-input') as HTMLInputElement;
   const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!;
   await act(async () => {
      setter.call(input, value);
      input.dispatchEvent(new Event('input', { bubbles: true }));
   });
};
const save = () => document.querySelector('#slug-save') as HTMLButtonElement;

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

test('isPlausibleSlug follows the rules of the server', () => {
   for (const ok of ['ada', 'ada-lovelace', 'dj-2k', '123']) expect(isPlausibleSlug(ok)).toBe(true);
   for (const bad of ['ab', '-ada', 'ada-', 'ada--x', 'ada x', 'Ada', 'ada_x', 'a'.repeat(31)])
      expect(isPlausibleSlug(bad)).toBe(false);
});

test('saving sends the name in lower case and hands on the new profile', async () => {
   const updated = profile({ slug: 'ada-lovelace' });
   api.setSlug.mockResolvedValue(updated);
   const onSaved = vi.fn();
   await act(async () => createRoot(container).render(<SlugSetup profile={profile()} onSaved={onSaved} />));

   expect(save().disabled).toBe(true);
   await type('  Ada-Lovelace ');
   expect(save().disabled).toBe(false);
   await act(async () => save().click());

   expect(api.setSlug).toHaveBeenCalledWith('ada-lovelace');
   expect(onSaved).toHaveBeenCalledWith(updated);
   expect(document.querySelector('#slug-saved')).not.toBeNull();
});

test('a name that cannot be valid is not sent', async () => {
   await act(async () => createRoot(container).render(<SlugSetup profile={profile()} onSaved={vi.fn()} />));

   await type('no good!');

   expect(save().disabled).toBe(true);
   expect(document.querySelector('#slug-input')?.closest('.MuiFormControl-root')?.textContent).toContain(
      'creator.slug_hint',
   );
});

test('the answer of the server (name taken) is shown', async () => {
   api.setSlug.mockRejectedValue(new Error('Another creator already has this name. Choose another one.'));
   await act(async () => createRoot(container).render(<SlugSetup profile={profile()} onSaved={vi.fn()} />));

   await type('ada');
   await act(async () => save().click());

   expect(document.querySelector('#slug-error')?.textContent).toContain('Another creator already has this name');
});

test('changing a name warns about the old links; the long link can be used again', async () => {
   api.setSlug.mockResolvedValue(profile());
   const onSaved = vi.fn();
   await act(async () =>
      createRoot(container).render(<SlugSetup profile={profile({ slug: 'ada' })} onSaved={onSaved} />),
   );

   expect(document.querySelector('#slug-change-warning')).toBeNull();
   await type('ada-l');
   expect(document.querySelector('#slug-change-warning')).not.toBeNull();

   await act(async () => (document.querySelector('#slug-clear') as HTMLElement).click());
   expect(api.setSlug).toHaveBeenCalledWith(null);
});

test('no clear button without a name', async () => {
   await act(async () => createRoot(container).render(<SlugSetup profile={profile()} onSaved={vi.fn()} />));

   expect(document.querySelector('#slug-clear')).toBeNull();
});
