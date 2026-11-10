import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }) }));

import Scoreboard from './Scoreboard';

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
});

const render = async (match: Record<string, unknown>) => {
   root = createRoot(container);
   await act(async () =>
      root!.render(<Scoreboard match={{ homeTeam: 'Lagos FC', awayTeam: 'Abuja United', ...match }} />),
   );
};

test('before the start: the two teams and "versus", no period', async () => {
   await render({ period: 'upcoming' });

   expect(container.textContent).toContain('Lagos FC');
   expect(container.textContent).toContain('Abuja United');
   expect(container.querySelector('.scoreboard-score')?.textContent).toBe('matches.versus');
   expect(container.querySelector('.scoreboard-period')).toBeNull();
});

test('while played: the score and the period, marked live in a half', async () => {
   await render({ homeScore: 2, awayScore: 1, period: '2h' });

   expect(container.querySelector('.scoreboard-score')?.textContent).toBe('2 : 1');
   expect(container.querySelector('.scoreboard-period')?.textContent).toContain('matches.period_2h');
   expect(container.querySelector('.scoreboard-period')?.textContent).toContain('●');
});

test('at full time: the score without the live mark', async () => {
   await render({ homeScore: 3, awayScore: 0, period: 'ft' });

   expect(container.querySelector('.scoreboard-score')?.textContent).toBe('3 : 0');
   expect(container.querySelector('.scoreboard-period')?.textContent).not.toContain('●');
});
