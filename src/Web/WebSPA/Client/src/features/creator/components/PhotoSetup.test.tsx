import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({ uploadPhoto: vi.fn(), removePhoto: vi.fn() }));
const resize = vi.hoisted(() => ({ shrinkToSquareJpeg: vi.fn(), isAcceptedPhoto: vi.fn() }));

vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }) }));
vi.mock('src/services/api/creators', () => api);
vi.mock('../errors', () => ({ requestErrorMessage: (e: unknown) => String((e as Error).message ?? e) }));
vi.mock('../resizePhoto', () => resize);

import PhotoSetup from './PhotoSetup';

const profile = (over: Record<string, unknown> = {}) =>
   ({ creatorId: 'c1', displayName: 'Ada Lovelace', currency: 'NGN', canSell: true, ...over } as any);

const pick = async (file: File) => {
   const input = document.querySelector('#photo-input') as HTMLInputElement;
   Object.defineProperty(input, 'files', { value: [file], configurable: true });
   await act(async () => {
      input.dispatchEvent(new Event('change', { bubbles: true }));
   });
};

let container: HTMLDivElement;
beforeEach(() => {
   container = document.createElement('div');
   document.body.appendChild(container);
   resize.isAcceptedPhoto.mockReturnValue(true);
});
afterEach(() => {
   container.remove();
   document.body.innerHTML = '';
   vi.clearAllMocks();
});

test('a chosen picture is shrunk first, and only the shrunk picture is sent', async () => {
   const shrunk = new Blob(['small'], { type: 'image/jpeg' });
   const updated = profile({ photoVersion: '123' });
   resize.shrinkToSquareJpeg.mockResolvedValue(shrunk);
   api.uploadPhoto.mockResolvedValue(updated);
   const onSaved = vi.fn();
   await act(async () => createRoot(container).render(<PhotoSetup profile={profile()} onSaved={onSaved} />));

   const original = new File(['a very big photo'], 'me.jpg', { type: 'image/jpeg' });
   await pick(original);

   expect(resize.shrinkToSquareJpeg).toHaveBeenCalledWith(original);
   expect(api.uploadPhoto).toHaveBeenCalledWith(shrunk);
   expect(onSaved).toHaveBeenCalledWith(updated);
});

test('a file that is no photo is not even read', async () => {
   resize.isAcceptedPhoto.mockReturnValue(false);
   await act(async () => createRoot(container).render(<PhotoSetup profile={profile()} onSaved={vi.fn()} />));

   await pick(new File(['<svg/>'], 'x.svg', { type: 'image/svg+xml' }));

   expect(resize.shrinkToSquareJpeg).not.toHaveBeenCalled();
   expect(api.uploadPhoto).not.toHaveBeenCalled();
   expect(document.querySelector('#photo-error')?.textContent).toContain('photo.wrong_file');
});

test('a picture the browser cannot read gets a friendly message; a refusal of the server is shown as it is', async () => {
   await act(async () => createRoot(container).render(<PhotoSetup profile={profile()} onSaved={vi.fn()} />));

   resize.shrinkToSquareJpeg.mockRejectedValueOnce(new Error('canvas'));
   await pick(new File(['x'], 'a.jpg', { type: 'image/jpeg' }));
   expect(document.querySelector('#photo-error')?.textContent).toContain('photo.unreadable');

   resize.shrinkToSquareJpeg.mockResolvedValueOnce(new Blob(['s'], { type: 'image/jpeg' }));
   api.uploadPhoto.mockRejectedValueOnce(new Error('Use a jpeg, png or webp picture.'));
   await pick(new File(['x'], 'a.jpg', { type: 'image/jpeg' }));
   expect(document.querySelector('#photo-error')?.textContent).toContain('Use a jpeg, png or webp picture.');
});

test('a photo can be removed; without one there is no remove button', async () => {
   const removed = profile();
   api.removePhoto.mockResolvedValue(removed);
   const onSaved = vi.fn();
   await act(async () =>
      createRoot(container).render(<PhotoSetup profile={profile({ photoVersion: '1' })} onSaved={onSaved} />),
   );

   expect(document.querySelector('#photo-choose')?.textContent).toContain('photo.change');
   await act(async () => (document.querySelector('#photo-remove') as HTMLElement).click());
   expect(onSaved).toHaveBeenCalledWith(removed);

   container.innerHTML = '';
   const second = document.createElement('div');
   document.body.appendChild(second);
   await act(async () => createRoot(second).render(<PhotoSetup profile={profile()} onSaved={vi.fn()} />));
   expect(document.querySelectorAll('#photo-remove')).toHaveLength(0);
});
