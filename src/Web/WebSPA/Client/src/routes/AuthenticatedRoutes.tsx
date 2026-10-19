import useUser from 'src/features/auth/useUser';
import Axios from 'axios';
import React, { useEffect, useLayoutEffect } from 'react';
import { useDispatch } from 'react-redux';
import { useAuth } from 'react-oidc-context';
import { Route, Switch } from 'react-router-dom';
import { setParticipantId } from 'src/features/auth/reducer';
import { refreshTokenOnUnauthorized } from 'src/features/auth/unauthorized';
import AdminRoute from './AdminRoute';
import ConferenceRoute from './ConferenceRoute';
import CreatorPage from 'src/features/creator/components/CreatorPage';
import MySessionsPage from 'src/features/sessions/components/MySessionsPage';
import MyTicketsPage from 'src/features/sessions/components/MyTicketsPage';
import MainRoute from './MainRoute';
import MyRecordingsRoute from './MyRecordingsRoute';

export default function AuthenticatedRoutes() {
   const oidcUser = useUser();
   const dispatch = useDispatch();
   const auth = useAuth();

   // a layout effect runs before the effects of the routes below, which already request the API
   useLayoutEffect(() => {
      dispatch(setParticipantId(oidcUser.profile.sub));
      Axios.defaults.headers.common = {
         Authorization: `Bearer ${oidcUser.access_token}`,
      };
   }, [oidcUser]);

   // a refused access token is renewed and the request repeated; if that is not possible the person signs in again
   useEffect(
      () =>
         refreshTokenOnUnauthorized(Axios, {
            renew: async () => (await auth.signinSilent())?.access_token,
            onSessionLost: () =>
               auth.signinRedirect({ state: { url: window.location.pathname + window.location.search } }),
         }),
      [auth.signinSilent, auth.signinRedirect],
   );

   return (
      <Switch>
         <Route exact path="/" component={MainRoute} />
         <Route path="/admin" component={AdminRoute} />
         <Route path="/creator/sessions" component={MySessionsPage} />
         <Route path="/creator" component={CreatorPage} />
         <Route path="/tickets" component={MyTicketsPage} />
         <Route path="/recordings" component={MyRecordingsRoute} />
         <Route path="/c/:id" component={ConferenceRoute} />
      </Switch>
   );
}
