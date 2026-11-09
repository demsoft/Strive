import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({ reportSession: vi.fn() }));

vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }) }));
vi.mock('src/services/api/sessions', () => api);
vi.mock('src/features/creator/errors', () => ({
   requestErrorMessage: (e: unknown) => String((e as Error).message ?? e),
}));

import ReportSessionDialog from './ReportSessionDialog';

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
   await act(async () => root!.render(<ReportSessionDialog sessionId="s1" open onClose={vi.fn()} />));
   await settle();
};

const choose = async (reason: string) => {
   await act(async () => {
      (document.querySelector('#session-report-reason') as HTMLElement).dispatchEvent(
         new MouseEvent('mousedown', { bubbles: true }),
      );
   });
   await act(async () => (document.querySelector(`[data-value="${reason}"]`) as HTMLElement).click());
};
const send = () => document.querySelector('#session-report-send') as HTMLButtonElement;

test('nothing can be sent before a reason is chosen', async () => {
   await render();

   expect(send().disabled).toBe(true);
});

test('the owner of the rights can say so, and the dialog says thanks', async () => {
   api.reportSession.mockResolvedValue(undefined);
   await render();

   await choose('rightsHolder');
   await act(async () => send().click());
   await settle();

   expect(api.reportSession).toHaveBeenCalledWith('s1', 'rightsHolder', '');
   expect(document.querySelector('#session-report-sent')).not.toBeNull();
});

test('a refusal of the server is shown', async () => {
   api.reportSession.mockRejectedValue(new Error('You cannot report your own session.'));
   await render();

   await choose('spam');
   await act(async () => send().click());
   await settle();

   expect(document.querySelector('#session-report-error')?.textContent).toContain('own session');
   expect(document.querySelector('#session-report-sent')).toBeNull();
});
