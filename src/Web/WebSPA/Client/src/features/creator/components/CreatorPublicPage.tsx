import { Alert, AppBar, Box, Button, CircularProgress, Container, Toolbar, Typography } from '@mui/material';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import React, { useCallback, useEffect, useState } from 'react';
import { useAuth } from 'react-oidc-context';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, useLocation, useParams } from 'react-router-dom';
import BrandLogo from 'src/components/BrandLogo';
import { SessionTile } from 'src/features/gbedu/DiscoverSessions';
import VideoTile from 'src/features/videos/components/VideoTile';
import { requestErrorMessage } from '../errors';
import { creatorPhotoUrl } from '../photoUrl';
import PassCard from './PassCard';
import HostAvatar from 'src/features/gbedu/ui/HostAvatar';
import * as api from 'src/services/api/sessions';
import * as follows from 'src/services/api/follows';
import FollowButton from 'src/features/social/FollowButton';
import NoticeSettings from 'src/features/social/NoticeSettings';
import { CreatorPage } from 'src/services/api/sessions';
import { brand } from 'src/theme';

/**
 * The page of a creator, the one link to share: who they are and the sessions they have on. Everybody can read it;
 * a session on it leads to its own page, where the ticket is bought.
 */
export default function CreatorPublicPage() {
   const { t } = useTranslation();
   const { creatorId } = useParams<{ creatorId: string }>();
   const auth = useAuth();
   const token = auth.user?.access_token;
   const location = useLocation();
   const [page, setPage] = useState<CreatorPage | null>(null);
   const [notFound, setNotFound] = useState(false);
   const [copied, setCopied] = useState(false);
   const [followError, setFollowError] = useState<string | null>(null);
   // what the person did on this page; none: what the server said when the page was loaded
   const [followed, setFollowed] = useState<boolean | null>(null);
   // the same for the mail the person asked for
   const [notice, setNotice] = useState<'off' | 'pending' | 'on' | null>(null);

   const load = useCallback(
      () => api.fetchCreatorPage(creatorId, token).then(setPage, () => setNotFound(true)),
      [creatorId, token],
   );

   useEffect(() => {
      if (auth.isLoading) return;
      load();
   }, [auth.isLoading, load]);

   useEffect(() => {
      setFollowed(null);
      setNotice(null);
   }, [page]);

   const serverFollowing = page?.following ?? false;
   const following = followed ?? serverFollowing;
   const followers = Math.max(0, (page?.followers ?? 0) + (following === serverFollowing ? 0 : following ? 1 : -1));

   const toggleFollow = async () => {
      if (!page) return;
      if (!token) return auth.signinRedirect({ state: { url: location.pathname } });
      const was = following;
      setFollowed(!was);
      // stopping to follow ends the mail as well
      if (was) setNotice('off');
      setFollowError(null);
      try {
         await (was ? follows.unfollowCreator(page.creator.creatorId) : follows.followCreator(page.creator.creatorId));
      } catch (e) {
         setFollowed(was);
         setFollowError(requestErrorMessage(e) || t('creators.follow_failed'));
      }
   };

   const copy = async () => {
      const link = api.creatorPageLink(page?.creator.slug || creatorId);
      try {
         await navigator.clipboard.writeText(link);
         setCopied(true);
      } catch {
         window.prompt(t('sessions.copy_manually'), link);
      }
   };

   return (
      <Box sx={{ minHeight: '100%' }}>
         <AppBar position="static" color="transparent" elevation={0}>
            <Toolbar>
               <RouterLink to="/" style={{ textDecoration: 'none', color: 'inherit' }}>
                  <BrandLogo size={30} />
               </RouterLink>
            </Toolbar>
         </AppBar>

         <Container maxWidth="md" sx={{ py: { xs: 2, sm: 4 } }} id="creator-public-page">
            {notFound && <Alert severity="error">{t('creator.page_not_found')}</Alert>}
            {!notFound && !page && (
               <Box sx={{ display: 'flex', justifyContent: 'center', p: 6 }}>
                  <CircularProgress />
               </Box>
            )}

            {page && (
               <>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 2.5, flexWrap: 'wrap', mb: 4 }}>
                     <HostAvatar
                        name={page.creator.displayName}
                        size={80}
                        background={brand.gradient}
                        photoUrl={creatorPhotoUrl(page.creator.creatorId, page.creator.photoVersion, 80)}
                     />
                     <Box sx={{ flex: 1, minWidth: 220 }}>
                        <Typography variant="h3" id="creator-name" sx={{ fontSize: { xs: '1.9rem', sm: '2.5rem' } }}>
                           {page.creator.displayName}
                        </Typography>
                        <Typography color="text.secondary" sx={{ mt: 0.5 }} id="creator-followers">
                           {t('social.followers', { count: followers })}
                        </Typography>
                        {page.creator.bio && (
                           <Typography color="text.secondary" sx={{ mt: 0.5 }} id="creator-bio">
                              {page.creator.bio}
                           </Typography>
                        )}
                     </Box>
                     {!page.isYou && (
                        <FollowButton className="creator-follow" following={following} onClick={toggleFollow} />
                     )}
                     <Button id="creator-share" variant="outlined" startIcon={<ContentCopyIcon />} onClick={copy}>
                        {copied ? t('sessions.copied') : t('creator.copy_page_link')}
                     </Button>
                  </Box>

                  {followError && (
                     <Alert severity="error" id="follow-error" sx={{ mb: 2 }} onClose={() => setFollowError(null)}>
                        {followError}
                     </Alert>
                  )}

                  {following && !page.isYou && page.noticesAvailable && (
                     <NoticeSettings
                        creatorId={page.creator.creatorId}
                        creatorName={page.creator.displayName}
                        state={notice ?? page.notice ?? 'off'}
                        onChange={setNotice}
                        knownEmail={auth.user?.profile?.email as string | undefined}
                     />
                  )}

                  <PassCard page={page} onPaid={load} />

                  {page.videos && page.videos.length > 0 && (
                     <Box id="creator-videos" sx={{ mb: 4 }}>
                        <Typography variant="h5" sx={{ fontWeight: 800, mb: 2 }}>
                           {t('videos.feed_title')}
                        </Typography>
                        <Box
                           sx={{
                              display: 'grid',
                              gap: 2,
                              gridTemplateColumns: { xs: 'repeat(2, 1fr)', sm: 'repeat(3, 1fr)' },
                           }}
                        >
                           {page.videos.map((video) => (
                              <VideoTile key={video.videoId} video={video} />
                           ))}
                        </Box>
                     </Box>
                  )}

                  <Typography variant="h5" sx={{ fontWeight: 800, mb: 2 }}>
                     {t('creator.page_sessions')}
                  </Typography>
                  {page.sessions.length === 0 ? (
                     <Typography color="text.secondary" id="creator-nothing-on">
                        {t('creator.page_nothing_on', { name: page.creator.displayName })}
                     </Typography>
                  ) : (
                     <Box
                        sx={{
                           display: 'grid',
                           gap: 2,
                           gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)' },
                        }}
                     >
                        {page.sessions.map((session) => (
                           <SessionTile key={session.sessionId} session={session} />
                        ))}
                     </Box>
                  )}
               </>
            )}
         </Container>
      </Box>
   );
}
