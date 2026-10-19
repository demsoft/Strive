import React from 'react';
import { hasAuthParams, useAuth } from 'react-oidc-context';
import RedirectToConference from 'src/RedirectToConference';
import RequireAuth from 'src/features/auth/components/RequireAuth';
import AuthenticatedRoutes from 'src/routes/AuthenticatedRoutes';
import GbeduLanding from './GbeduLanding';

/**
 * The start page of Gbedu: a visitor who is not signed in sees the welcome page with the sign up and sign in buttons
 * (everything else asks for a sign in at once); a signed in person gets the app. While the sign in state is not known,
 * or when the sign in comes back with its answer, the normal handling takes over.
 */
export default function LandingGate() {
   const auth = useAuth();

   const showLanding =
      !auth.isLoading && !auth.isAuthenticated && !auth.activeNavigator && !auth.error && !hasAuthParams();
   if (showLanding) return <GbeduLanding />;

   return (
      <RequireAuth>
         <AuthenticatedRoutes />
         <RedirectToConference />
      </RequireAuth>
   );
}
