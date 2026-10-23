import React, { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({ startEmailNotices: vi.fn(), stopEmailNotices: vi.fn() }));

vi.mock('react-i18next', () => ({
   useTranslation: () => ({
      t: (key: string, values?: Record<string, unknown>) => (values ? `${key} ${JSON.stringify(values)}` : key),
   }),
}));
vi.mock('src/services/api/follows', () => api);
vi.mock('src/features/creator/errors', () => ({
   requestErrorMessage: (e: unknown) => String((e as Error).message ?? e),
}));

import NoticeSettings from './NoticeSettings';

let container: HTMLDivElement;
let root: Root;
const onChange = vi.fn();
const settle = () => act(async () => undefined);
const render = async (state: 'off' | 'pending' | 'on', knownEmail?: string) => {
   await act(async () =>
      root.render(
         <NoticeSettings creatorId="c1" creatorName="Ada" state={state} onChange={onChange} knownEmail={knownEmail} />,
      ),
   );
   await settle();
};
const click = (selector: string) => act(async () => document.querySelector<HTMLElement>(selector)?.click());
const type = async (value: string) => {
   const box = document.querySelector<HTMLInputElement>('#notice-email')!;
   const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!;
   await act(async () => {
      setter.call(box, value);
      box.dispatchEvent(new Event('input', { bubbles: true }));
   });
};

beforeEach(() => {
   container = document.createElement('div');
   document.body.appendChild(container);
   root = createRoot(container);
   api.startEmailNotices.mockResolvedValue(undefined);
   api.stopEmailNotices.mockResolvedValue(undefined);
});
afterEach(() => {
   act(() => root.unmount());
   container.remove();
   document.body.innerHTML = '';
   vi.clearAllMocks();
});

test('off: a button to ask for mail; the dialog starts with the address of the account', async () => {
   await render('off', 'ada@example.com');

   await click('#notice-ask');

   expect((document.querySelector('#notice-email') as HTMLInputElement).value).toBe('ada@example.com');
});

test('sending the address asks the server and shows that the confirmation is awaited', async () => {
   await render('off');
   await click('#notice-ask');
   await type('  kofi@example.com ');

   await click('#notice-send');

   expect(api.startEmailNotices).toHaveBeenCalledWith('c1', 'kofi@example.com');
   expect(onChange).toHaveBeenCalledWith('pending');
});

test('an address that is not one is refused before anything is sent', async () => {
   await render('off');
   await click('#notice-ask');
   await type('nope');

   await click('#notice-send');

   expect(api.startEmailNotices).not.toHaveBeenCalled();
   expect(document.querySelector('#notice-error')?.textContent).toBe('social.notice_invalid');
});

test('when the server refuses its message is shown and nothing changes', async () => {
   api.startEmailNotices.mockRejectedValue(new Error('The mail could not be sent.'));
   await render('off');
   await click('#notice-ask');
   await type('kofi@example.com');

   await click('#notice-send');

   expect(document.querySelector('#notice-error')?.textContent).toContain('The mail could not be sent.');
   expect(onChange).not.toHaveBeenCalled();
});

test('pending: says to check the inbox and offers another address', async () => {
   await render('pending');

   expect(document.querySelector('#notice-pending')?.textContent).toContain('social.notice_pending');
   expect(document.querySelector('#notice-pending button')).not.toBeNull();
});

test('on: says so, and stopping asks the server', async () => {
   await render('on');
   expect(document.querySelector('#notice-on')?.textContent).toContain('social.notice_on');

   await click('#notice-stop');

   expect(api.stopEmailNotices).toHaveBeenCalledWith('c1');
   expect(onChange).toHaveBeenCalledWith('off');
});
