import React, { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({ fetchFollowing: vi.fn(), followCreator: vi.fn(), unfollowCreator: vi.fn() }));
vi.mock('src/services/api/follows', () => api);

import useFollowing from './useFollowing';

type Following = ReturnType<typeof useFollowing>;
let current: Following;
const Probe = ({ token }: { token?: string }) => {
   current = useFollowing(token);
   return null;
};

let container: HTMLDivElement;
let root: Root;
const settle = () => act(async () => undefined);
const render = async (token?: string) => {
   await act(async () => root.render(<Probe token={token} />));
   await settle();
};

beforeEach(() => {
   container = document.createElement('div');
   document.body.appendChild(container);
   root = createRoot(container);
   api.fetchFollowing.mockResolvedValue([{ creatorId: 'c1' }, { creatorId: 'c2' }]);
   api.followCreator.mockResolvedValue(undefined);
   api.unfollowCreator.mockResolvedValue(undefined);
});
afterEach(() => {
   act(() => root.unmount());
   container.remove();
   vi.clearAllMocks();
});

test('nobody is followed while nobody is signed in, and nothing is asked for', async () => {
   await render(undefined);

   expect(api.fetchFollowing).not.toHaveBeenCalled();
   expect(current.isFollowing('c1')).toBe(false);
   await act(async () => current.toggle('c1'));
   expect(api.followCreator).not.toHaveBeenCalled();
});

test('a signed in person gets the creators they follow', async () => {
   await render('tok');

   expect(current.isFollowing('c1')).toBe(true);
   expect(current.isFollowing('c3')).toBe(false);
});

test('toggling follows or stops following, and is seen at once', async () => {
   await render('tok');

   await act(async () => current.toggle('c3'));
   expect(api.followCreator).toHaveBeenCalledWith('c3');
   expect(current.isFollowing('c3')).toBe(true);

   await act(async () => current.toggle('c1'));
   expect(api.unfollowCreator).toHaveBeenCalledWith('c1');
   expect(current.isFollowing('c1')).toBe(false);
});

test('when the server refuses the button goes back', async () => {
   api.followCreator.mockRejectedValue(new Error('down'));
   await render('tok');

   await act(async () => current.toggle('c3'));

   expect(current.isFollowing('c3')).toBe(false);
});

test('signing out forgets whom the person followed', async () => {
   await render('tok');
   await render(undefined);

   expect(current.isFollowing('c1')).toBe(false);
});
