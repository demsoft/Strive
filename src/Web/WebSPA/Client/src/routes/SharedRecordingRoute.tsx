import { Alert, Box, Button, CircularProgress, Typography } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import { DateTime } from 'luxon';
import React, { useEffect, useRef, useState } from 'react';
import { useAuth } from 'react-oidc-context';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router-dom';
import BrandLogo from 'src/components/BrandLogo';
import * as api from 'src/services/api/recording';
import { SharedRecordingDto } from 'src/services/api/recording';
import { brand } from 'src/theme';
import to from 'src/utils/to';

const useStyles = makeStyles()((theme) => ({
   root: { minHeight: '100%', display: 'flex', flexDirection: 'column', padding: theme.spacing(2, 3), boxSizing: 'border-box' },
   body: { flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: theme.spacing(3, 0) },
   card: {
      width: '100%',
      maxWidth: 960,
      textAlign: 'center',
   },
   video: {
      width: '100%',
      maxHeight: '70vh',
      borderRadius: 20,
      backgroundColor: '#000',
      border: `1px solid ${brand.border}`,
      boxShadow: '0 24px 80px rgba(0, 0, 0, 0.45)',
   },
}));

type State =
   | { type: 'loading' }
   | { type: 'ready'; recording: SharedRecordingDto }
   | { type: 'login-required' }
   | { type: 'not-found' }
   | { type: 'error' };

/**
 * The page behind a recording's share link. Recordings shared with everybody play without signing in, the others
 * ask for a sign in first.
 */
export default function SharedRecordingRoute() {
   const { classes } = useStyles();
   const { t } = useTranslation();
   const auth = useAuth();
   const { token } = useParams<{ token: string }>();
   const [state, setState] = useState<State>({ type: 'loading' });
   const refreshTimer = useRef<ReturnType<typeof setTimeout>>();

   const accessToken = auth.user?.access_token;

   useEffect(() => {
      // wait until the sign in state is known, otherwise a signed in user would be asked to sign in
      if (auth.isLoading) return;

      let cancelled = false;

      const load = async () => {
         try {
            const recording = await api.fetchShared(token, accessToken);
            if (cancelled) return;
            setState({ type: 'ready', recording });

            // the playback link expires, get a new one shortly before (the player keeps what it has buffered)
            const wait = new Date(recording.urlExpiresAt).getTime() - Date.now() - 2 * 60 * 1000;
            refreshTimer.current = setTimeout(load, Math.max(30 * 1000, wait));
         } catch (error) {
            if (cancelled) return;
            const status = (error as { response?: { status?: number } }).response?.status;
            setState({ type: status === 401 ? 'login-required' : status === 404 ? 'not-found' : 'error' });
         }
      };

      load();
      return () => {
         cancelled = true;
         if (refreshTimer.current) clearTimeout(refreshTimer.current);
      };
   }, [token, accessToken, auth.isLoading]);

   const handleSignIn = () => auth.signinRedirect({ state: { url: window.location.pathname } });

   return (
      <div className={classes.root} id="shared-recording">
         <header>
            <Link {...to('/')} style={{ textDecoration: 'none', color: 'inherit' }}>
               <BrandLogo size={30} />
            </Link>
         </header>
         <div className={classes.body}>
            <div className={classes.card}>
               {state.type === 'loading' && <CircularProgress />}
               {state.type === 'ready' && (
                  <>
                     <video
                        key={state.recording.url.split('?')[0]}
                        className={classes.video}
                        src={state.recording.url}
                        controls
                        autoPlay={false}
                        playsInline
                        id="shared-recording-video"
                     />
                     <Box sx={{ mt: 2 }}>
                        <Typography variant="h6">
                           {state.recording.conferenceName || t('conference.recording.untitled')}
                        </Typography>
                        <Typography color="textSecondary">
                           {DateTime.fromISO(state.recording.startedAt).toLocaleString(DateTime.DATETIME_MED)}
                        </Typography>
                     </Box>
                  </>
               )}
               {state.type === 'login-required' && (
                  <Box id="shared-recording-login">
                     <Typography variant="h4" gutterBottom>
                        {t('conference.recording.login_required_title')}
                     </Typography>
                     <Typography color="textSecondary" sx={{ mb: 3 }}>
                        {t('conference.recording.login_required_text')}
                     </Typography>
                     <Button variant="contained" size="large" onClick={handleSignIn}>
                        {t('conference.recording.sign_in')}
                     </Button>
                  </Box>
               )}
               {state.type === 'not-found' && (
                  <Box id="shared-recording-not-found">
                     <Typography variant="h4" gutterBottom>
                        {t('conference.recording.not_found_title')}
                     </Typography>
                     <Typography color="textSecondary">{t('conference.recording.not_found_text')}</Typography>
                  </Box>
               )}
               {state.type === 'error' && <Alert severity="error">{t('conference.recording.load_error')}</Alert>}
            </div>
         </div>
      </div>
   );
}
