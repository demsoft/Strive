import React, { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({ likeVideo: vi.fn(), unlikeVideo: vi.fn() }));
vi.mock('src/services/api/videos', () => api);

import useVideoLike from './useVideoLike';

let current: ReturnType<typeof useVideoLike>;
const signIn = vi.fn();
const Probe = ({ id = 'v1', likes = 3, liked = false, signedIn = true }) => {
   current = useVideoLike(id, { likes, liked }, signedIn, signIn);
   return null;
};

let container: HTMLDivElement;
let root: Root;
const render = (props: Parameters<typeof Probe>[0] = {}) => act(async () => root.render(<Probe {...props} />));

beforeEach(() => {
   container = document.createElement('div');
   document.body.appendChild(container);
   root = createRoot(container);
   api.likeVideo.mockResolvedValue({ likes: 4, liked: true });
   api.unlikeVideo.mockResolvedValue({ likes: 2, liked: false });
});
afterEach(() => {
   act(() => root.unmount());
   container.remove();
   vi.clearAllMocks();
});

test('starts with what the server said', async () => {
   await render({ likes: 7, liked: true });

   expect(current.likes).toBe(7);
   expect(current.liked).toBe(true);
});

test('liking shows the heart at once and then the number the server gives', async () => {
   let resolve: (v: { likes: number; liked: boolean }) => void = () => undefined;
   api.likeVideo.mockReturnValue(new Promise((r) => (resolve = r)));
   await render();

   act(() => void current.toggle());
   expect(current.liked).toBe(true);
   expect(current.likes).toBe(4);

   // others liked it meanwhile
   await act(async () => resolve({ likes: 9, liked: true }));
   expect(current.likes).toBe(9);
   expect(api.likeVideo).toHaveBeenCalledWith('v1');
});

test('pressing again takes the like back', async () => {
   await render({ likes: 3, liked: true });

   await act(async () => current.toggle());

   expect(api.unlikeVideo).toHaveBeenCalledWith('v1');
   expect(current.liked).toBe(false);
   expect(current.likes).toBe(2);
});

test('when the server refuses it goes back', async () => {
   api.likeVideo.mockRejectedValue(new Error('down'));
   await render();

   await act(async () => current.toggle());

   expect(current.liked).toBe(false);
   expect(current.likes).toBe(3);
});

test('signed out: nothing is sent, the person is asked to sign in', async () => {
   await render({ signedIn: false });

   await act(async () => current.toggle());

   expect(signIn).toHaveBeenCalledTimes(1);
   expect(api.likeVideo).not.toHaveBeenCalled();
   expect(current.liked).toBe(false);
});

test('another video starts again from its own numbers', async () => {
   await render({ id: 'v1', likes: 3 });
   await render({ id: 'v2', likes: 10, liked: true });

   expect(current.likes).toBe(10);
   expect(current.liked).toBe(true);
});
