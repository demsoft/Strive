import { User } from 'oidc-client-ts';
import React from 'react';
import { AuthProvider } from 'react-oidc-context';
import { useHistory } from 'react-router-dom';
import { ocidConfig } from 'src/config';

export type SigninState = { url?: string };

type Props = {
   children?: React.ReactNode;
};

/**
 * OIDC provider. After the sign in callback the user is navigated back to the page they came from
 * (passed as state by RequireAuth), which also removes the code and state parameters from the url.
 */
export default function StriveAuthProvider({ children }: Props) {
   const history = useHistory();

   const handleSigninCallback = (user: User | void) => {
      const state = user?.state as SigninState | undefined;
      history.replace(state?.url ?? '/');
   };

   return (
      <AuthProvider {...ocidConfig} onSigninCallback={handleSigninCallback}>
         {children}
      </AuthProvider>
   );
}
