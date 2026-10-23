import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

import HostAvatar from './HostAvatar';

let container: HTMLDivElement;
beforeEach(() => {
   container = document.createElement('div');
   document.body.appendChild(container);
});
afterEach(() => {
   container.remove();
   document.body.innerHTML = '';
});

test('without a photo the initials are shown', async () => {
   await act(async () => createRoot(container).render(<HostAvatar name="Ada Lovelace" />));

   expect(container.querySelector('img')).toBeNull();
   expect(container.textContent).toBe('AL');
});

test('with a photo the picture is shown, named after the host', async () => {
   await act(async () =>
      createRoot(container).render(
         <HostAvatar name="Ada Lovelace" photoUrl="https://api.test/v1/creators/c1/photo?v=1" />,
      ),
   );

   const image = container.querySelector('img')!;
   expect(image.getAttribute('src')).toBe('https://api.test/v1/creators/c1/photo?v=1');
   expect(container.querySelector('[aria-label="Ada Lovelace"]')).not.toBeNull();
});
