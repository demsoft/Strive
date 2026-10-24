import React, { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

vi.mock('react-i18next', () => ({
   useTranslation: () => ({
      t: (key: string, values?: Record<string, unknown>) => (values ? `${key} ${JSON.stringify(values)}` : key),
   }),
}));
vi.mock('react-router-dom', () => ({
   Link: React.forwardRef<HTMLAnchorElement, { to: string; children: React.ReactNode; className?: string }>(
      ({ to, children, className }, ref) => (
         <a ref={ref} href={to} className={className}>
            {children}
         </a>
      ),
   ),
}));

import SetupChecklist, { SetupStep } from './SetupChecklist';

const step = (id: string, over: Partial<SetupStep> = {}): SetupStep => ({
   id,
   title: 'Step ' + id,
   text: 'About ' + id,
   done: false,
   action: 'Do ' + id,
   scrollTo: 'target-' + id,
   ...over,
});

let container: HTMLDivElement;
let root: Root;
const render = (steps: SetupStep[]) => act(async () => root.render(<SetupChecklist steps={steps} />));

beforeEach(() => {
   container = document.createElement('div');
   document.body.appendChild(container);
   root = createRoot(container);
});
afterEach(() => {
   act(() => root.unmount());
   container.remove();
   document.body.innerHTML = '';
});

test('says how many are done, and only the open steps have a button', async () => {
   await render([step('a', { done: true }), step('b'), step('c', { to: '/somewhere' })]);

   expect(document.querySelector('.setup-progress-text')?.textContent).toContain('{"done":1,"total":3}');
   expect(document.querySelectorAll('.setup-step.done')).toHaveLength(1);
   expect(document.querySelectorAll('.setup-action')).toHaveLength(2);
   // a step with a page leads there
   expect(document.querySelector('a.setup-action')?.getAttribute('href')).toBe('/somewhere');
});

test('a button without a page scrolls to the part of the page where it is done', async () => {
   const target = document.createElement('div');
   target.id = 'target-b';
   target.scrollIntoView = vi.fn();
   document.body.appendChild(target);
   await render([step('b')]);

   await act(async () => document.querySelector<HTMLElement>('button.setup-action')?.click());

   expect(target.scrollIntoView).toHaveBeenCalled();
});

test('an optional step is not counted, and when everything that is needed is done the card goes away', async () => {
   await render([step('a', { done: true }), step('b'), step('p', { optional: true })]);
   expect(document.querySelector('.setup-progress-text')?.textContent).toContain('{"done":1,"total":2}');

   await render([step('a', { done: true }), step('b', { done: true }), step('p', { optional: true })]);
   expect(document.querySelector('#setup-checklist')).toBeNull();
});
