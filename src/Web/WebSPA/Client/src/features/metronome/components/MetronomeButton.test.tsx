import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const dispatch = vi.fn();
let canControl = false;
let session: { bpm: number; beatsPerBar: number; startedBy: string; firstBeatAt: string } | null = null;

vi.mock('react-redux', () => ({
   useDispatch: () => dispatch,
   useSelector: (selector: string) => (selector === 'session' ? session : selector === 'volume' ? 0.7 : false),
}));
vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }) }));
vi.mock('src/hooks/usePermission', () => ({ default: () => canControl }));
vi.mock('src/features/watch-together/useServerClock', () => ({ default: () => () => Date.now() }));
vi.mock('../useMetronomeClick', () => ({ default: () => null }));
vi.mock('../selectors', () => ({ selectMetronomeSession: 'session' }));
vi.mock('src/features/settings/selectors', () => ({ selectMetronomeVolume: 'volume', selectMetronomeMuted: 'muted' }));
vi.mock('src/features/settings/reducer', () => ({
   setMetronomeMuted: (payload: boolean) => ({ type: 'setMetronomeMuted', payload }),
   setMetronomeVolume: (payload: number) => ({ type: 'setMetronomeVolume', payload }),
}));
vi.mock('src/core-hub', () => ({
   startMetronome: (payload: unknown) => ({ type: 'startMetronome', payload }),
   stopMetronome: () => ({ type: 'stopMetronome' }),
}));

import MetronomeButton from './MetronomeButton';

let container: HTMLDivElement;

beforeEach(() => {
   dispatch.mockReset();
   container = document.createElement('div');
   document.body.appendChild(container);
});
afterEach(() => {
   container.remove();
   document.body.innerHTML = '';
});

const render = (controller: boolean, running: boolean) => {
   canControl = controller;
   session = running ? { bpm: 96, beatsPerBar: 3, startedBy: 't', firstBeatAt: new Date().toISOString() } : null;
   act(() => createRoot(container).render(<MetronomeButton />));
};
const click = (selector: string) => act(() => (document.querySelector(selector) as HTMLElement).click());

test('people who cannot control it see nothing while no click runs', () => {
   render(false, false);

   expect(container.innerHTML).toBe('');
});

test('everybody gets the button while the click runs, to set the volume on their computer', () => {
   render(false, true);

   click('#metronome-button');

   expect(document.querySelector('#metronome-mute')).not.toBeNull();
   expect(document.querySelector('#metronome-controls')).toBeNull();
   expect(document.querySelectorAll('.metronome-dot')).toHaveLength(3);
});

test('a teacher starts the click with the tempo and the beats of the bar', () => {
   render(true, false);
   click('#metronome-button');

   click('#metronome-start');

   expect(dispatch).toHaveBeenCalledWith({ type: 'startMetronome', payload: { bpm: 100, beatsPerBar: 4 } });
});

test('a teacher stops it', () => {
   render(true, true);
   click('#metronome-button');

   click('#metronome-stop');

   expect(dispatch).toHaveBeenCalledWith({ type: 'stopMetronome' });
});

test('turning the click off on this computer does not stop it for the others', () => {
   render(false, true);
   click('#metronome-button');

   click('#metronome-mute');

   expect(dispatch).toHaveBeenCalledWith({ type: 'setMetronomeMuted', payload: true });
   expect(dispatch).not.toHaveBeenCalledWith({ type: 'stopMetronome' });
});
