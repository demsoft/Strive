import useUser from 'src/features/auth/useUser';
import Axios from 'axios';
import React, { useLayoutEffect } from 'react';
import { useDispatch } from 'react-redux';
import { Route, Switch } from 'react-router-dom';
import { setParticipantId } from 'src/features/auth/reducer';
import AdminRoute from './AdminRoute';
import ConferenceRoute from './ConferenceRoute';
import MainRoute from './MainRoute';

export default function AuthenticatedRoutes() {
   const oidcUser = useUser();
   const dispatch = useDispatch();

   // a layout effect runs before the effects of the routes below, which already request the API
   useLayoutEffect(() => {
      dispatch(setParticipantId(oidcUser.profile.sub));
      Axios.defaults.headers.common = {
         Authorization: `Bearer ${oidcUser.access_token}`,
      };
   }, [oidcUser]);

   return (
      <Switch>
         <Route exact path="/" component={MainRoute} />
         <Route path="/admin" component={AdminRoute} />
         <Route path="/c/:id" component={ConferenceRoute} />
      </Switch>
   );
}
