import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const dispatch = vi.fn();
let canControl = false;
let active = false;

vi.mock('react-redux', () => ({ useDispatch: () => dispatch, useSelector: () => active }));
vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }) }));
vi.mock('src/hooks/usePermission', () => ({ default: () => canControl }));
vi.mock('src/core-hub', () => ({ setLessonMode: (payload: unknown) => ({ type: 'setLessonMode', payload }) }));

import LessonModeButton from './LessonModeButton';

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

const render = (controller: boolean, lessonActive: boolean) => {
   canControl = controller;
   active = lessonActive;
   act(() => createRoot(container).render(<LessonModeButton />));
};

test('a teacher turns the lesson mode on with one click', () => {
   render(true, false);

   act(() => (document.querySelector('#lesson-mode-button') as HTMLElement).click());

   expect(dispatch).toHaveBeenCalledWith({ type: 'setLessonMode', payload: { active: true } });
});

test('a teacher turns the lesson mode off again', () => {
   render(true, true);

   act(() => (document.querySelector('#lesson-mode-button') as HTMLElement).click());

   expect(dispatch).toHaveBeenCalledWith({ type: 'setLessonMode', payload: { active: false } });
});

test('the others get no button, only a note while the lesson mode is on', () => {
   render(false, true);

   expect(document.querySelector('#lesson-mode-button')).toBeNull();
   expect(document.querySelector('#lesson-mode-chip')).not.toBeNull();
});

test('the others see nothing while it is off', () => {
   render(false, false);

   expect(container.innerHTML).toBe('');
});
