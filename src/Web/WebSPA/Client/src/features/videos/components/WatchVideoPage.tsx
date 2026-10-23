import { Alert, AppBar, Box, Button, CircularProgress, Container, Toolbar, Typography } from '@mui/material';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import { DateTime } from 'luxon';
import React, { useCallback, useEffect, useState } from 'react';
import { useAuth } from 'react-oidc-context';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, useLocation, useParams } from 'react-router-dom';
import BrandLogo from 'src/components/BrandLogo';
import { creatorPhotoUrl } from 'src/features/creator/photoUrl';
import HostAvatar from 'src/features/gbedu/ui/HostAvatar';
import * as api from 'src/services/api/videos';
import { Video } from 'src/services/api/videos';
import usePaymentReturn from 'src/features/payments/usePaymentReturn';
import VideoBuyBox from './VideoBuyBox';
import { brand } from 'src/theme';

/** The page of one video, the one that is shared: it plays, says who made it and leads to the creator. */
export default function WatchVideoPage() {
   const { t } = useTranslation();
   const { videoId } = useParams<{ videoId: string }>();
   const auth = useAuth();
   const token = auth.user?.access_token;
   const location = useLocation();
   const [video, setVideo] = useState<Video | null>(null);
   const [notFound, setNotFound] = useState(false);
   const [copied, setCopied] = useState(false);

   const load = useCallback(
      () => api.fetchVideo(videoId, token).then(setVideo, () => setNotFound(true)),
      [videoId, token],
   );

   useEffect(() => {
      // wait until it is known whether the person is signed in: the answer depends on it
      if (auth.isLoading) return;
      load();
   }, [auth.isLoading, load]);

   // back from the payment page: when it was paid the video is loaded again, and now it plays
   const payment = usePaymentReturn(new URLSearchParams(location.search).get('reference'), token, load);

   const share = async () => {
      const link = api.videoLink(videoId);
      try {
         await navigator.clipboard.writeText(link);
         setCopied(true);
      } catch {
         window.prompt(t('sessions.copy_manually'), link);
      }
   };

   const creator = video?.creator;

   return (
      <Box sx={{ minHeight: '100%' }}>
         <AppBar position="static" color="transparent" elevation={0}>
            <Toolbar>
               <RouterLink to="/" style={{ textDecoration: 'none', color: 'inherit' }}>
                  <BrandLogo size={30} />
               </RouterLink>
            </Toolbar>
         </AppBar>

         <Container maxWidth="sm" sx={{ py: { xs: 1, sm: 3 } }} id="watch-video-page">
            {notFound && (
               <Alert severity="error" id="watch-not-found">
                  {t('videos.not_found')}
               </Alert>
            )}
            {!notFound && !video && (
               <Box sx={{ display: 'flex', justifyContent: 'center', p: 6 }}>
                  <CircularProgress />
               </Box>
            )}

            {video && (
               <>
                  {video.locked ? (
                     <VideoBuyBox
                        video={video}
                        accessToken={token}
                        knownEmail={auth.user?.profile?.email as string | undefined}
                        payment={payment}
                        onSignIn={() => auth.signinRedirect({ state: { url: location.pathname } })}
                        onSignUp={() => auth.signinRedirect({ state: { url: location.pathname }, prompt: 'create' })}
                     />
                  ) : video.playbackUrl ? (
                     <video
                        id="video-player"
                        src={video.playbackUrl}
                        poster={video.posterUrl ?? undefined}
                        controls
                        playsInline
                        autoPlay
                        preload="metadata"
                        style={{ width: '100%', maxHeight: '75vh', borderRadius: 16, backgroundColor: '#000' }}
                     />
                  ) : (
                     <Alert severity="info" id="watch-not-ready">
                        {t('videos.not_ready')}
                     </Alert>
                  )}

                  <Typography variant="h5" sx={{ fontWeight: 800, mt: 2 }} id="watch-title">
                     {video.title}
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                     {DateTime.fromISO(video.createdAt).toLocaleString(DateTime.DATE_MED)}
                  </Typography>

                  {creator && (
                     <Box
                        id="watch-creator"
                        component={RouterLink}
                        to={`/u/${creator.slug || creator.creatorId}`}
                        sx={{
                           display: 'flex',
                           alignItems: 'center',
                           gap: 1.5,
                           mt: 2,
                           p: 1.5,
                           borderRadius: '16px',
                           textDecoration: 'none',
                           color: 'inherit',
                           backgroundColor: brand.surface,
                           border: `1px solid ${brand.border}`,
                        }}
                     >
                        <HostAvatar
                           name={creator.displayName}
                           size={44}
                           background={brand.gradient}
                           photoUrl={creatorPhotoUrl(creator.creatorId, creator.photoVersion, 44)}
                        />
                        <Box sx={{ minWidth: 0, flex: 1 }}>
                           <Typography sx={{ fontWeight: 700 }}>{creator.displayName}</Typography>
                           <Typography variant="body2" color="text.secondary">
                              {t('videos.see_creator')}
                           </Typography>
                        </Box>
                     </Box>
                  )}

                  {video.description && (
                     <Typography sx={{ mt: 2, whiteSpace: 'pre-wrap' }} id="watch-description">
                        {video.description}
                     </Typography>
                  )}

                  <Button
                     id="watch-share"
                     variant="outlined"
                     startIcon={<ContentCopyIcon />}
                     onClick={share}
                     sx={{ mt: 2 }}
                  >
                     {copied ? t('sessions.copied') : t('videos.share')}
                  </Button>
               </>
            )}
         </Container>
      </Box>
   );
}
