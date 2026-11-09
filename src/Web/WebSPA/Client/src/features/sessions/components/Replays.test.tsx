import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({ fetchReplays: vi.fn() }));

vi.mock('react-i18next', () => ({
   useTranslation: () => ({
      t: (key: string, values?: Record<string, unknown>) => (values ? `${key} ${JSON.stringify(values)}` : key),
   }),
}));
vi.mock('src/services/api/sessions', () => api);

import Replays from './Replays';

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
   await act(async () => root!.render(<Replays sessionId="s1" />));
   await settle();
};

const replay = (id: string, over: Record<string, unknown> = {}) => ({
   recordingId: id,
   startedAt: '2026-11-14T16:00:00Z',
   durationSeconds: 5400,
   url: `https://storage.test/${id}.mp4`,
   urlExpiresAt: '2026-11-14T20:00:00Z',
   ...over,
});

test('nothing is shown without recordings, or when the person may not watch', async () => {
   api.fetchReplays.mockResolvedValue([]);
   await render();
   expect(document.querySelector('#session-replays')).toBeNull();

   await act(async () => root!.unmount());
   root = null;
   api.fetchReplays.mockRejectedValue({ response: { status: 403 } });
   await render();
   expect(document.querySelector('#session-replays')).toBeNull();
});

test('recordings are listed with their length and play when chosen', async () => {
   api.fetchReplays.mockResolvedValue([replay('r1'), replay('r2', { durationSeconds: null })]);
   await render();

   const rows = document.querySelectorAll('.replay');
   expect(rows).toHaveLength(2);
   expect(rows[0].textContent).toContain('"count":90');
   expect(document.querySelector('#replay-player')).toBeNull();

   await act(async () => (rows[1] as HTMLElement).click());

   expect((document.querySelector('#replay-player') as HTMLVideoElement).src).toBe('https://storage.test/r2.mp4');
});
