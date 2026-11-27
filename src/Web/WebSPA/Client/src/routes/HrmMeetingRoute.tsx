import React, { useEffect, useLayoutEffect, useMemo, useState } from 'react';
import { RouteComponentProps } from 'react-router-dom';
import { AuthContext, AuthContextProps } from 'react-oidc-context';
import { User, UserManager, WebStorageStateStore, InMemoryWebStorage } from 'oidc-client-ts';
import axios from 'axios';
import { useDispatch } from 'react-redux';
import { Alert, Button, CircularProgress, Stack, Typography } from '@mui/material';
import settings from 'src/config';
import { MeetingPortalContext } from 'src/features/hrm-call/MeetingPortalContext';
import { receiveHrmCallSession } from 'src/features/hrm-call/handoff';
import { setParticipantId } from 'src/features/auth/reducer';
import ConferenceRoute from './ConferenceRoute';
import { close } from 'src/store/signal/actions';

type Props = RouteComponentProps<{ id: string }>;
type Session = { accessToken: string; subject: string; name: string; expiresAt: string; parentOrigin: string };

export default function HrmMeetingRoute(props: Props) {
   const id = props.match.params.id;
   const [session, setSession] = useState<Session>();
   const [error, setError] = useState('');
   useEffect(() => {
      let disposed = false;
      let cancel: (() => void) | undefined;
      const controller = new AbortController();
      if (!/^hrm-[a-f0-9]{32}$/.test(id)) { setError('This meeting link is invalid.'); return; }
      void (async () => {
         try {
            const requestedParent = new URLSearchParams(window.location.search).get('parentOrigin');
            const query = requestedParent ? '?parentOrigin=' + encodeURIComponent(requestedParent) : '';
            const response = await fetch(settings.conferenceUrl + '/v1/hrm/meetings/' + id + '/bootstrap' + query, { signal: controller.signal });
            if (!response.ok) throw new Error('This meeting is not available. Open it from HRM.');
            const config = await response.json();
            if (disposed) return;
            if (!config.enabled) throw new Error('GOS MEET is not connected to HRM on this server.');
            const origin = new URL(config.parentOrigin);
            if (origin.protocol !== 'https:' || origin.origin !== config.parentOrigin) throw new Error('The HRM connection is not configured correctly.');
            if (window.parent === window) {
               window.location.replace(origin.origin + '/gos-meet/' + encodeURIComponent(id));
               return;
            }
            cancel = receiveHrmCallSession({
               parentOrigin: origin.origin, conferenceId: id, organizationId: config.organizationId, channel: 'meeting',
               onTimeout: () => { if (!disposed) setError('HRM could not sign you in to this meeting. Close it and join again.'); },
               onSession: handoff => {
                  void axios.post<Session>(settings.conferenceUrl + '/v1/hrm/meetings/' + id + '/redeem', { ticket: handoff.token })
                     .then(result => { if (!disposed) setSession(result.data); })
                     .catch(() => { if (!disposed) setError('Your meeting handoff expired. Close it and join again.'); });
               }
            });
         } catch (cause) {
            if (!disposed) setError(cause instanceof Error ? cause.message : 'Could not open this meeting.');
         }
      })();
      return () => { disposed = true; controller.abort(); cancel?.(); };
   }, [id]);
   if (error) return <Stack spacing={2} sx={{ p: 3 }}><Alert severity="error">{error}</Alert></Stack>;
   if (!session) return <Stack spacing={2} sx={{ p: 4, alignItems: 'center' }}><CircularProgress /><Typography>Opening GOS MEET with your HRM account…</Typography></Stack>;
   return <HrmMeetingSession session={session}><ConferenceRoute {...props} /></HrmMeetingSession>;
}

function HrmMeetingSession({ session, children }: { session: Session; children: React.ReactNode }) {
   const dispatch = useDispatch();
   const [ended, setEnded] = useState(false);
   const leave = () => { dispatch(close()); setEnded(true); window.parent.postMessage({ type: 'goshrm.meeting.left' }, session.parentOrigin); };
   const user = useMemo(() => new User({ access_token: session.accessToken, token_type: 'Bearer',
      profile: { sub: session.subject, name: session.name, iss: 'goshrm-scheduled-meeting', aud: 'gos-meet-hrm',
         exp: Math.floor(new Date(session.expiresAt).getTime() / 1000), iat: Math.floor(Date.now() / 1000) },
      expires_at: Math.floor(new Date(session.expiresAt).getTime() / 1000) }), [session]);
   const auth = useMemo<AuthContextProps>(() => {
      const cannotSignIn = async (): Promise<User> => { throw new Error('Join this meeting from your HRM account.'); };
      return {
         user, isLoading: false, isAuthenticated: true,
         settings: { authority: '', client_id: 'hrm-meeting', redirect_uri: '' },
         events: new UserManager({ authority: settings.conferenceUrl, client_id: 'hrm-meeting', redirect_uri: window.location.origin,
            stateStore: new WebStorageStateStore({ store: new InMemoryWebStorage() }), userStore: new WebStorageStateStore({ store: new InMemoryWebStorage() }) }).events,
         clearStaleState: async () => {}, removeUser: async () => leave(),
         signinPopup: cannotSignIn, signinSilent: async () => null, signinRedirect: async () => leave(),
         signinResourceOwnerCredentials: cannotSignIn,
         signoutRedirect: async () => leave(), signoutPopup: async () => leave(), signoutSilent: async () => leave(),
         querySessionStatus: async () => null, revokeTokens: async () => leave(),
         startSilentRenew: () => {}, stopSilentRenew: () => {},
      };
   }, [user]);
   useLayoutEffect(() => {
      dispatch(setParticipantId(session.subject));
      const apiOrigin = new URL(settings.conferenceUrl).origin;
      const interceptor = axios.interceptors.request.use(request => {
         const target = new URL(request.url ?? '', request.baseURL ?? window.location.origin);
         const apiPath = new URL(settings.conferenceUrl).pathname.replace(/\/$/, '') + '/v1/';
         if (target.origin === apiOrigin && target.pathname.startsWith(apiPath)) request.headers = { ...request.headers, Authorization: `Bearer ${session.accessToken}` };
         return request;
      });
      return () => { axios.interceptors.request.eject(interceptor); dispatch(close()); };
   }, [session]);
   useEffect(() => {
      const timer = setTimeout(leave, Math.max(0, new Date(session.expiresAt).getTime() - Date.now()));
      return () => clearTimeout(timer);
   }, [session]);
   if (ended) return <Stack spacing={2} sx={{ p: 3 }}><Typography>You have left the meeting.</Typography><Button onClick={() => window.parent.postMessage({ type: 'goshrm.meeting.left' }, session.parentOrigin)}>Return to HRM</Button></Stack>;
   return <MeetingPortalContext.Provider value={session.parentOrigin}><AuthContext.Provider value={auth}>{children}</AuthContext.Provider></MeetingPortalContext.Provider>;
}
