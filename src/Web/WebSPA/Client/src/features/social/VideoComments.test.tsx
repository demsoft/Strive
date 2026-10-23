import React, { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({
   fetchComments: vi.fn(),
   postComment: vi.fn(),
   deleteComment: vi.fn(),
   MAX_COMMENT_LENGTH: 500,
}));

vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }) }));
vi.mock('src/services/api/videos', () => api);
vi.mock('src/features/creator/errors', () => ({
   requestErrorMessage: (e: unknown) => String((e as Error).message ?? e),
}));

import VideoComments from './VideoComments';

const comment = (id: string, over: Record<string, unknown> = {}) => ({
   commentId: id,
   authorName: 'Tola',
   text: 'Text ' + id,
   createdAt: '2026-10-20T10:00:00Z',
   mine: false,
   canDelete: false,
   ...over,
});

let container: HTMLDivElement;
let root: Root;
const onSignIn = vi.fn();
const onTotal = vi.fn();
const settle = () => act(async () => undefined);
const texts = () => [...document.querySelectorAll('.comment-text')].map((x) => x.textContent);
const render = async (props: Partial<React.ComponentProps<typeof VideoComments>> = {}) => {
   await act(async () =>
      root.render(<VideoComments videoId="v1" signedIn onSignIn={onSignIn} onTotal={onTotal} {...props} />),
   );
   await settle();
};
const type = async (value: string) => {
   const box = document.querySelector<HTMLTextAreaElement>('#comment-text')!;
   const setter = Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value')!.set!;
   await act(async () => {
      setter.call(box, value);
      box.dispatchEvent(new Event('input', { bubbles: true }));
   });
};
const click = (selector: string) => act(async () => document.querySelector<HTMLElement>(selector)?.click());

beforeEach(() => {
   container = document.createElement('div');
   document.body.appendChild(container);
   root = createRoot(container);
   api.fetchComments.mockResolvedValue({ items: [comment('a'), comment('b')], next: null, total: 2 });
});
afterEach(() => {
   act(() => root.unmount());
   container.remove();
   document.body.innerHTML = '';
   vi.clearAllMocks();
});

test('shows the comments as they come and tells the total', async () => {
   await render();

   expect(api.fetchComments).toHaveBeenCalledWith('v1');
   expect(texts()).toEqual(['Text a', 'Text b']);
   expect(document.querySelector('.comments-total')?.textContent).toBe('2');
   expect(onTotal).toHaveBeenCalledWith(2);
});

test('no comments: an invitation to write the first', async () => {
   api.fetchComments.mockResolvedValue({ items: [], next: null, total: 0 });
   await render();

   expect(document.querySelector('#comments-none')).not.toBeNull();
});

test('a failure to load is said', async () => {
   api.fetchComments.mockRejectedValue(new Error('down'));
   await render();

   expect(document.querySelector('#comments-failed')).not.toBeNull();
});

test('signed out there is no box, only a way to sign in', async () => {
   await render({ signedIn: false });

   expect(document.querySelector('#comment-text')).toBeNull();
   await click('#comment-sign-in');
   expect(onSignIn).toHaveBeenCalledTimes(1);
});

test('under a video that costs money, for somebody who has not paid, nothing can be written', async () => {
   await render({ locked: true });

   expect(document.querySelector('#comments-locked')).not.toBeNull();
   expect(document.querySelector('#comment-text')).toBeNull();
   expect(document.querySelector('#comment-sign-in')).toBeNull();
});

test('writing sends the text, shows it on top, empties the box and tells the new total', async () => {
   api.postComment.mockResolvedValue(comment('new', { text: 'Lovely', mine: true, canDelete: true }));
   await render();
   expect(document.querySelector<HTMLButtonElement>('#comment-send')?.disabled).toBe(true);

   await type('  Lovely ');
   await click('#comment-send');

   expect(api.postComment).toHaveBeenCalledWith('v1', 'Lovely');
   expect(texts()).toEqual(['Lovely', 'Text a', 'Text b']);
   expect(document.querySelector<HTMLTextAreaElement>('#comment-text')?.value).toBe('');
   expect(onTotal).toHaveBeenLastCalledWith(3);
});

test('when the server refuses, its message is shown and the text stays', async () => {
   api.postComment.mockRejectedValue(new Error('Slow down a little'));
   await render();
   await type('Again');

   await click('#comment-send');

   expect(document.querySelector('#comments-error')?.textContent).toContain('Slow down a little');
   expect(document.querySelector<HTMLTextAreaElement>('#comment-text')?.value).toBe('Again');
   expect(texts()).toEqual(['Text a', 'Text b']);
});

test('only what may be taken away has a delete button, and taking it away removes it and lowers the total', async () => {
   api.fetchComments.mockResolvedValue({
      items: [comment('a', { canDelete: true }), comment('b')],
      next: null,
      total: 2,
   });
   api.deleteComment.mockResolvedValue(undefined);
   await render();
   expect(document.querySelectorAll('.comment-delete')).toHaveLength(1);

   await click('.comment-delete');

   expect(api.deleteComment).toHaveBeenCalledWith('v1', 'a');
   expect(texts()).toEqual(['Text b']);
   expect(onTotal).toHaveBeenLastCalledWith(1);
});

test('older comments are loaded on request, without repeating any', async () => {
   api.fetchComments
      .mockResolvedValueOnce({ items: [comment('a'), comment('b')], next: '2026-10-20T09:00:00Z', total: 3 })
      .mockResolvedValueOnce({ items: [comment('b'), comment('c')], next: null, total: 3 });
   await render();

   await click('#comments-older');

   expect(api.fetchComments).toHaveBeenLastCalledWith('v1', '2026-10-20T09:00:00Z');
   expect(texts()).toEqual(['Text a', 'Text b', 'Text c']);
   expect(document.querySelector('#comments-older')).toBeNull();
});

test('another video starts again with its own comments', async () => {
   await render();
   api.fetchComments.mockResolvedValue({ items: [comment('z')], next: null, total: 1 });

   await render({ videoId: 'v2' });

   expect(api.fetchComments).toHaveBeenLastCalledWith('v2');
   expect(texts()).toEqual(['Text z']);
});
