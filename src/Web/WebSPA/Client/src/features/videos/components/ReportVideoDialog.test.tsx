import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({ reportVideo: vi.fn() }));

vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }) }));
vi.mock('src/services/api/videos', () => api);
vi.mock('src/features/creator/errors', () => ({
   requestErrorMessage: (e: unknown) => String((e as Error).message ?? e),
}));

import ReportVideoDialog from './ReportVideoDialog';

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

const render = async (open = true, onClose = vi.fn()) => {
   root = createRoot(container);
   await act(async () => root!.render(<ReportVideoDialog videoId="v1" open={open} onClose={onClose} />));
   await settle();
   return onClose;
};

const choose = async (reason: string) => {
   await act(async () => {
      (document.querySelector('#report-reason') as HTMLElement).dispatchEvent(
         new MouseEvent('mousedown', { bubbles: true }),
      );
   });
   const option = document.querySelector(`[data-value="${reason}"]`) as HTMLElement;
   await act(async () => option.click());
};
const type = async (value: string) => {
   const input = document.querySelector('#report-details') as HTMLTextAreaElement;
   const setter = Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value')!.set!;
   await act(async () => {
      setter.call(input, value);
      input.dispatchEvent(new Event('input', { bubbles: true }));
   });
};
const send = () => document.querySelector('#report-send') as HTMLButtonElement;

test('nothing can be sent before a reason is chosen', async () => {
   await render();

   expect(send().disabled).toBe(true);
});

test('the reason and the words are sent, and then the dialog says thanks', async () => {
   api.reportVideo.mockResolvedValue(undefined);
   await render();

   await choose('copyright');
   await type('  It is from another channel  ');
   expect(send().disabled).toBe(false);
   await act(async () => send().click());
   await settle();

   expect(api.reportVideo).toHaveBeenCalledWith('v1', 'copyright', 'It is from another channel');
   expect(document.querySelector('#report-sent')).not.toBeNull();
   expect(document.querySelector('#report-send')).toBeNull();
});

test('a refusal of the server is shown and the report can be sent again', async () => {
   api.reportVideo.mockRejectedValue(new Error('You cannot report your own video.'));
   await render();

   await choose('spam');
   await act(async () => send().click());
   await settle();

   expect(document.querySelector('#report-error')?.textContent).toContain('You cannot report your own video.');
   expect(send().disabled).toBe(false);
});

test('the words are held to 500 characters', async () => {
   await render();

   await type('x'.repeat(700));

   expect((document.querySelector('#report-details') as HTMLTextAreaElement).value).toHaveLength(500);
});

test('closed, nothing is shown', async () => {
   await render(false);

   expect(document.querySelector('#report-reason')).toBeNull();
});
