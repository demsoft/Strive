import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({ fetchLiveMatch: vi.fn(), setScoreboard: vi.fn() }));
const state = vi.hoisted(() => ({ conferenceId: 'room1' as string | null }));

vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }) }));
vi.mock('react-redux', () => ({
   useSelector: (select: (s: unknown) => unknown) => select({ conference: { conferenceId: state.conferenceId } }),
}));
vi.mock('src/services/api/sessions', () => api);

import RoomScoreboard, { SCOREBOARD_REFRESH_MS } from './RoomScoreboard';

let container: HTMLDivElement;
let root: ReturnType<typeof createRoot> | null = null;
beforeEach(() => {
   vi.useFakeTimers();
   container = document.createElement('div');
   document.body.appendChild(container);
});
afterEach(async () => {
   await act(async () => root?.unmount());
   root = null;
   container.remove();
   document.body.innerHTML = '';
   vi.useRealTimers();
   vi.clearAllMocks();
});

const live = (over: Record<string, unknown> = {}) => ({
   sessionId: 's1',
   title: 'Lagos FC vs Abuja United',
   isHost: false,
   match: { homeTeam: 'Lagos FC', awayTeam: 'Abuja United', homeScore: 1, awayScore: 0, period: '1h' },
   ...over,
});
const render = async () => {
   root = createRoot(container);
   await act(async () => root!.render(<RoomScoreboard />));
};

test('a room that is not a match shows nothing', async () => {
   api.fetchLiveMatch.mockResolvedValue(null);
   await render();

   expect(container.querySelector('#room-scoreboard')).toBeNull();
});

test('a fan sees the score and it is asked again every few seconds, without the buttons', async () => {
   api.fetchLiveMatch
      .mockResolvedValueOnce(live())
      .mockResolvedValue(
         live({ match: { homeTeam: 'Lagos FC', awayTeam: 'Abuja United', homeScore: 2, awayScore: 0, period: '1h' } }),
      );
   await render();
   expect(api.fetchLiveMatch).toHaveBeenCalledWith('room1');
   expect(container.querySelector('.scoreboard-score')?.textContent).toBe('1 : 0');
   expect(container.querySelector('#scoreboard-controls')).toBeNull();

   await act(async () => {
      await vi.advanceTimersByTimeAsync(SCOREBOARD_REFRESH_MS);
   });

   expect(container.querySelector('.scoreboard-score')?.textContent).toBe('2 : 0');
});

test('the organizer gets the buttons', async () => {
   api.fetchLiveMatch.mockResolvedValue(live({ isHost: true }));
   await render();

   expect(container.querySelector('#scoreboard-controls')).not.toBeNull();
});
