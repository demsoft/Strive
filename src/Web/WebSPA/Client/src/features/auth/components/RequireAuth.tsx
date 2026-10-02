import React, { useEffect, useRef } from 'react';
import { hasAuthParams, useAuth } from 'react-oidc-context';
import { useLocation } from 'react-router-dom';
import AuthCallback from './AuthCallback';
import AuthenticatingComponent from './AuthenticatingComponent';
import NotAuthenticated from './NotAuthenticated';
import SessionLostComponent from './SessionLostComponent';
import { SigninState } from './StriveAuthProvider';

type Props = {
   children?: React.ReactNode;
};

/**
 * Render the children only for an authenticated user, else start the sign in redirect.
 */
export default function RequireAuth({ children }: Props) {
   const auth = useAuth();
   const location = useLocation();
   const wasAuthenticated = useRef(false);

   if (auth.isAuthenticated) wasAuthenticated.current = true;

   const shouldSignIn =
      !hasAuthParams() && !auth.isAuthenticated && !auth.activeNavigator && !auth.isLoading && !auth.error;

   useEffect(() => {
      if (!shouldSignIn) return;

      const state: SigninState = { url: location.pathname + location.search };
      auth.signinRedirect({ state });
   }, [shouldSignIn]);

   if (auth.error) return wasAuthenticated.current ? <SessionLostComponent /> : <NotAuthenticated />;
   if (hasAuthParams() && !auth.isAuthenticated) return <AuthCallback />;
   if (!auth.isAuthenticated) return <AuthenticatingComponent />;

   return <>{children}</>;
}
