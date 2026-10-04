import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const push = vi.fn();
const dispatch = vi.fn();
let canEnd = false;

vi.mock('react-router-dom', () => ({ useHistory: () => ({ push }) }));
vi.mock('react-redux', () => ({ useDispatch: () => dispatch }));
vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }) }));
vi.mock('src/hooks/usePermission', () => ({ default: () => canEnd }));
vi.mock('src/core-hub', () => ({ closeConference: () => ({ type: 'closeConference' }) }));

import LeaveConferenceButton from './LeaveConferenceButton';

let container: HTMLDivElement;

beforeEach(() => {
   push.mockReset();
   dispatch.mockReset();
   container = document.createElement('div');
   document.body.appendChild(container);
});
afterEach(() => {
   container.remove();
   document.body.innerHTML = '';
});

const render = (moderator: boolean) => {
   canEnd = moderator;
   act(() => createRoot(container).render(<LeaveConferenceButton />));
};
const click = (selector: string) => act(() => (document.querySelector(selector) as HTMLElement).click());

test('the button asks before leaving: nothing happens at the first tap', () => {
   render(false);

   click('#leave-conference-button');

   expect(document.querySelector('#leave-conference-dialog')).not.toBeNull();
   expect(push).not.toHaveBeenCalled();
});

test('leaving goes back to the start page', () => {
   render(false);
   click('#leave-conference-button');

   click('#leave-conference-confirm');

   expect(push).toHaveBeenCalledWith('/');
   expect(dispatch).not.toHaveBeenCalled();
});

test('a participant can only leave, not end the meeting', () => {
   render(false);
   click('#leave-conference-button');

   expect(document.querySelector('#end-conference-button')).toBeNull();
});

test('a moderator can end the meeting for everybody, and then leaves too', () => {
   render(true);
   click('#leave-conference-button');

   click('#end-conference-button');

   expect(dispatch).toHaveBeenCalledWith({ type: 'closeConference' });
   expect(push).toHaveBeenCalledWith('/');
});

test('a moderator can leave and the meeting continues', () => {
   render(true);
   click('#leave-conference-button');

   click('#leave-conference-confirm');

   expect(push).toHaveBeenCalledWith('/');
   expect(dispatch).not.toHaveBeenCalled();
});
