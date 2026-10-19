import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const dispatch = vi.hoisted(() => vi.fn());
const store = vi.hoisted(() => ({
   participants: {} as Record<string, { displayName: string }>,
   canOpen: true,
   sound: false,
}));

vi.mock('react-redux', () => ({
   useDispatch: () => dispatch,
   useSelector: (selector: unknown) =>
      (selector as (s: unknown) => unknown)({
         conference: { participants: { participants: store.participants } },
         settings: { obj: { conference: { playSoundOnOpen: store.sound } } },
         auth: { participantId: 'me' },
      }),
}));
vi.mock('react-i18next', () => ({
   useTranslation: () => ({
      t: (key: string, values?: Record<string, unknown>) => (values ? `${key} ${JSON.stringify(values)}` : key),
   }),
}));
vi.mock('react-router-dom', () => ({ useParams: () => ({ id: 'room1' }) }));
vi.mock('src/components/BrandLogo', () => ({ default: () => null }));
vi.mock('src/utils/to', () => ({ default: () => ({}) }));
vi.mock('src/hooks/usePermission', () => ({ default: () => store.canOpen }));
vi.mock('src/features/conference/components/conference-not-open/ConferenceOpenSound', () => ({ default: () => null }));
vi.mock('src/core-hub', () => ({ openConference: () => ({ type: 'openConference' }) }));
vi.mock('src/features/create-conference/reducer', () => ({
   openDialogToPatchAsync: (id: string) => ({ type: 'patch', id }),
}));
vi.mock('src/features/settings/reducer', () => ({
   openSettings: () => ({ type: 'openSettings' }),
   setPlaySoundOnOpen: (v: boolean) => ({ type: 'sound', v }),
}));

import GbeduWaitingRoom from './GbeduWaitingRoom';

const info = { name: 'Piano for beginners', moderators: ['host'], isOpen: false, scheduledDate: null } as never;
let container: HTMLDivElement;
const render = (isHost: boolean) =>
   act(async () => createRoot(container).render(<GbeduWaitingRoom conferenceInfo={info} isHost={isHost} />));

beforeEach(() => {
   dispatch.mockReset();
   store.participants = { me: { displayName: 'Me' } };
   store.canOpen = true;
   container = document.createElement('div');
   document.body.appendChild(container);
});
afterEach(() => {
   container.remove();
   document.body.innerHTML = '';
});

test('the audience waits, nothing to refresh, no button to start and no link to share', async () => {
   await render(false);

   expect(document.querySelector('#waiting-title')?.textContent).toBe('gbedu_live.waiting_title');
   expect(document.querySelector('#moderator-open-conference-button')).toBeNull();
   expect(container.textContent).toContain('gbedu_live.no_refresh');
   expect(container.textContent).not.toContain('/c/room1');
});

test('the audience is told when the host is already here', async () => {
   store.participants = { me: { displayName: 'Me' }, host: { displayName: 'Ada' } };

   await render(false);

   expect(container.textContent).toContain('gbedu_live.host_is_here');
});

test('the host starts the session with one button, and sees how many people wait', async () => {
   store.participants = { me: { displayName: 'Me' }, a: { displayName: 'A' }, b: { displayName: 'B' } };
   await render(true);

   expect(document.querySelector('#waiting-title')?.textContent).toBe('gbedu_live.ready_title');
   expect(document.querySelector('#waiting-count')?.textContent).toContain('"count":2');
   await act(async () => (document.querySelector('#moderator-open-conference-button') as HTMLElement).click());
   expect(dispatch).toHaveBeenCalledWith({ type: 'openConference' });
});

test('recorders do not count as people who wait', async () => {
   store.participants = { me: { displayName: 'Me' }, 'recorder-abc': { displayName: 'Recording' } };

   await render(true);

   expect(document.querySelector('#waiting-count')).toBeNull();
});
