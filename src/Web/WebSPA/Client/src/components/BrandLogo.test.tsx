import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

import BrandLogo from './BrandLogo';

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
});

const render = async (element: React.ReactElement) => {
   root = createRoot(container);
   await act(async () => root!.render(element));
   return container.querySelector('img') as HTMLImageElement;
};

test('the word mark keeps its shape: both sides are set, so that a column layout cannot stretch it', async () => {
   const image = await render(<BrandLogo size={30} />);

   expect(image.alt).toBe('GOS Meet');
   const style = getComputedStyle(image);
   expect(style.width).toBe('145px');
   expect(style.height).toBe('27px');
   expect(style.objectFit).toBe('contain');
});

test('without the name it is the square ring', async () => {
   const image = await render(<BrandLogo size={48} showName={false} />);

   const style = getComputedStyle(image);
   expect(style.width).toBe('48px');
   expect(style.height).toBe('48px');
});
