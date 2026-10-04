import { configureStore } from '@reduxjs/toolkit';
import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { AuthContext } from 'react-oidc-context';
import { Provider } from 'react-redux';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import authReducer, { setParticipantId } from 'src/features/auth/reducer';
import useMyParticipantId from './useMyParticipantId';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

let container: HTMLDivElement;
beforeEach(() => {
   container = document.createElement('div');
   document.body.appendChild(container);
});
afterEach(() => container.remove());

function Probe({ onId }: { onId: (id: string) => void }) {
   onId(useMyParticipantId());
   return null;
}

function render(participantId: string | null, auth?: object) {
   const store = configureStore({ reducer: { auth: authReducer } });
   if (participantId) store.dispatch(setParticipantId(participantId));

   const ids: string[] = [];
   const probe = <Probe onId={(id) => ids.push(id)} />;
   const root = createRoot(container);
   act(() => {
      root.render(
         <Provider store={store}>
            {auth ? <AuthContext.Provider value={auth as never}>{probe}</AuthContext.Provider> : probe}
         </Provider>,
      );
   });
   return ids;
}

test('the recorder has no sign in, its participant id comes from the state', () => {
   expect(render('recorder-rec1')).toEqual(['recorder-rec1']);
});

test('a signed in user is identified by the login, as before', () => {
   expect(render('something-else', { user: { profile: { sub: 'user1' } } })).toEqual(['user1']);
});

test('without a login and without an id in the state it is an error, not a wrong id', () => {
   const error = vi.spyOn(console, 'error').mockImplementation(() => undefined);

   expect(() => render(null)).toThrowError(/needs a signed in user/);

   error.mockRestore();
});
