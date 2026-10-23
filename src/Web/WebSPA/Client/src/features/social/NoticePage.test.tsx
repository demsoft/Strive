import React, { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({ confirmNotice: vi.fn(), stopNotice: vi.fn() }));
const route = vi.hoisted(() => ({ action: 'confirm', token: 'abc123' }));

vi.mock('react-i18next', () => ({
   useTranslation: () => ({
      t: (key: string, values?: Record<string, unknown>) => (values ? `${key} ${JSON.stringify(values)}` : key),
   }),
}));
vi.mock('react-router-dom', () => ({
   Link: React.forwardRef<HTMLAnchorElement, { to: string; children: React.ReactNode }>(({ to, children }, ref) => (
      <a ref={ref} href={to}>
         {children}
      </a>
   )),
   useParams: () => route,
}));
vi.mock('src/components/BrandLogo', () => ({ default: () => null }));
vi.mock('src/services/api/follows', () => api);

import NoticePage from './NoticePage';

let container: HTMLDivElement;
let root: Root;
const render = async () => {
   await act(async () => root.render(<NoticePage />));
   await act(async () => undefined);
};

beforeEach(() => {
   container = document.createElement('div');
   document.body.appendChild(container);
   root = createRoot(container);
   route.action = 'confirm';
});
afterEach(() => {
   act(() => root.unmount());
   container.remove();
   document.body.innerHTML = '';
   vi.clearAllMocks();
});

test('the confirm link confirms the address and says so', async () => {
   api.confirmNotice.mockResolvedValue({ creatorName: 'Ada', status: 'on' });
   await render();

   expect(api.confirmNotice).toHaveBeenCalledWith('abc123');
   expect(document.querySelector('#notice-done')?.textContent).toContain('social.notice_page_confirmed {"name":"Ada"}');
});

test('the stop link stops the mail and says so', async () => {
   route.action = 'stop';
   api.stopNotice.mockResolvedValue({ creatorName: 'Ada', status: 'off' });
   await render();

   expect(api.stopNotice).toHaveBeenCalledWith('abc123');
   expect(document.querySelector('#notice-done')?.textContent).toContain('social.notice_page_stopped {"name":"Ada"}');
});

test('a link that is not valid any more says so', async () => {
   api.confirmNotice.mockRejectedValue(new Error('404'));
   await render();

   expect(document.querySelector('#notice-invalid')).not.toBeNull();
   expect(document.querySelector('#notice-done')).toBeNull();
});

test('an unknown action does not call the server', async () => {
   route.action = 'something';
   await render();

   expect(document.querySelector('#notice-invalid')).not.toBeNull();
   expect(api.confirmNotice).not.toHaveBeenCalled();
   expect(api.stopNotice).not.toHaveBeenCalled();
});
