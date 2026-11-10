import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({ setScoreboard: vi.fn() }));

vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }) }));
vi.mock('src/services/api/sessions', () => api);

import ScoreboardControls from './ScoreboardControls';

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

const match = (over: Record<string, unknown> = {}) => ({ homeTeam: 'Lagos FC', awayTeam: 'Abuja United', ...over });
const render = async (m = match(), onChanged = vi.fn()) => {
   root = createRoot(container);
   await act(async () => root!.render(<ScoreboardControls sessionId="s1" match={m as any} onChanged={onChanged} />));
   return onChanged;
};
const click = (id: string) => act(async () => (container.querySelector(id) as HTMLElement).click());

test('a goal starts the first half if the match had not started, and is sent at once', async () => {
   const updated = match({ homeScore: 1, awayScore: 0, period: '1h' });
   api.setScoreboard.mockResolvedValue(updated);
   const onChanged = await render();

   await click('#score-home-plus');

   expect(api.setScoreboard).toHaveBeenCalledWith('s1', 1, 0, '1h');
   expect(onChanged).toHaveBeenCalledWith(updated);
});

test('a goal can be taken back, but the score never goes below zero', async () => {
   api.setScoreboard.mockResolvedValue(match());
   await render(match({ homeScore: 0, awayScore: 2, period: '2h' }));

   expect((container.querySelector('#score-home-minus') as HTMLButtonElement).disabled).toBe(true);
   await click('#score-away-minus');

   expect(api.setScoreboard).toHaveBeenCalledWith('s1', 0, 1, '2h');
});

test('the period buttons change only the period', async () => {
   api.setScoreboard.mockResolvedValue(match());
   await render(match({ homeScore: 2, awayScore: 2, period: '2h' }));

   await click('#period-ft');

   expect(api.setScoreboard).toHaveBeenCalledWith('s1', 2, 2, 'ft');
});

test('a failure is shown', async () => {
   api.setScoreboard.mockRejectedValue(new Error('x'));
   await render(match({ period: '1h' }));

   await click('#period-ht');

   expect(container.querySelector('#scoreboard-failed')).not.toBeNull();
});
