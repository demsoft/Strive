import React, { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

vi.mock('react-router-dom', () => ({
   Link: React.forwardRef<
      HTMLAnchorElement,
      { to: string; children: React.ReactNode; id?: string; className?: string }
   >(({ to, children, id, className }, ref) => (
      <a ref={ref} href={to} id={id} className={className}>
         {children}
      </a>
   )),
}));

import ActionCard from './ActionCard';

let container: HTMLDivElement;
let root: Root;

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

test('a card leads to its page', async () => {
   await act(async () =>
      root.render(<ActionCard id="go" title="Videos" text="Upload" icon={<span />} to="/creator/videos" />),
   );

   expect(document.querySelector('#go')?.getAttribute('href')).toBe('/creator/videos');
   expect(document.querySelector('#go')?.textContent).toContain('Upload');
});

test('a locked card says what is missing and does not lead there: it does what it is told', async () => {
   const onLockedClick = vi.fn();
   await act(async () =>
      root.render(
         <ActionCard
            id="go"
            title="Earnings"
            text="Money"
            icon={<span />}
            to="/creator/earnings"
            locked
            lockedText="Add your bank account first."
            onLockedClick={onLockedClick}
         />,
      ),
   );

   const card = document.querySelector('#go') as HTMLElement;
   expect(card.tagName).toBe('BUTTON');
   expect(card.textContent).toContain('Add your bank account first.');
   expect(card.textContent).not.toContain('Money');
   await act(async () => card.click());
   expect(onLockedClick).toHaveBeenCalledTimes(1);
});
