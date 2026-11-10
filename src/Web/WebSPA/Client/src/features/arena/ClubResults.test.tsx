import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }) }));
vi.mock('react-router-dom', () => ({
   Link: ({ children, className }: { children: React.ReactNode; className?: string }) => (
      <a className={className}>{children}</a>
   ),
}));

import ClubResults from './ClubResults';

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

test('nothing is shown without results', async () => {
   root = createRoot(container);
   await act(async () => root!.render(<ClubResults results={[]} />));

   expect(container.querySelector('#club-results')).toBeNull();
});

test('each result shows the teams and the final score', async () => {
   root = createRoot(container);
   await act(async () =>
      root!.render(
         <ClubResults
            results={[
               {
                  sessionId: 's1',
                  title: 'x',
                  startsAt: '2026-11-01T16:00:00Z',
                  match: {
                     homeTeam: 'Lagos FC',
                     awayTeam: 'Abuja United',
                     competition: 'State Cup',
                     homeScore: 3,
                     awayScore: 1,
                     period: 'ft',
                  },
               },
            ]}
         />,
      ),
   );

   const row = container.querySelector('.club-result')!;
   expect(row.textContent).toContain('Lagos FC');
   expect(row.textContent).toContain('Abuja United');
   expect(row.querySelector('.club-result-score')?.textContent).toBe('3 : 1');
});
