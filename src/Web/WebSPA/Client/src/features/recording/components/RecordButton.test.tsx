import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const dispatch = vi.fn();

vi.mock('react-redux', () => ({
   useDispatch: () => dispatch,
   // the selectors: recording is available and nothing is recorded right now
   useSelector: (selector: unknown) => (selector === 'available' ? true : null),
}));
vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }) }));
vi.mock('src/hooks/usePermission', () => ({ default: () => true }));
vi.mock('src/hooks/useIsMobile', () => ({ default: () => false }));
vi.mock('../selectors', () => ({ selectIsRecordingAvailable: 'available', selectActiveRecording: 'active' }));
vi.mock('../reducer', () => ({ openRecordings: () => ({ type: 'openRecordings' }) }));
vi.mock('src/core-hub', () => ({
   startRecording: () => ({ type: 'startRecording' }),
   startRehearsalRecording: () => ({ type: 'startRehearsalRecording' }),
   stopRecording: () => ({ type: 'stopRecording' }),
}));

import RecordButton from './RecordButton';

let container: HTMLDivElement;

beforeEach(() => {
   dispatch.mockReset();
   container = document.createElement('div');
   document.body.appendChild(container);
   act(() => createRoot(container).render(<RecordButton />));
   act(() => (document.querySelector('#recording-toggle') as HTMLElement).click());
});
afterEach(() => {
   container.remove();
   document.body.innerHTML = '';
});

const click = (selector: string) => act(() => (document.querySelector(selector) as HTMLElement).click());

test('the dialog offers the meeting video and the rehearsal side by side', () => {
   expect(document.querySelector('#recording-confirm-start')).not.toBeNull();
   expect(document.querySelector('#recording-confirm-start-rehearsal')).not.toBeNull();
   expect(dispatch).not.toHaveBeenCalled();
});

test('the meeting is recorded as before', () => {
   click('#recording-confirm-start');

   expect(dispatch).toHaveBeenCalledWith({ type: 'startRecording' });
});

test('a rehearsal is recorded as sound with tracks', () => {
   click('#recording-confirm-start-rehearsal');

   expect(dispatch).toHaveBeenCalledWith({ type: 'startRehearsalRecording' });
});
