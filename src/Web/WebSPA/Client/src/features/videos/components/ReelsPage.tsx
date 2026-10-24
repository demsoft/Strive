import {
   Alert,
   Box,
   Button,
   CircularProgress,
   Drawer,
   IconButton,
   Skeleton,
   Snackbar,
   Typography,
   useMediaQuery,
} from '@mui/material';
import CloseIcon from '@mui/icons-material/Close';
import ExploreOutlinedIcon from '@mui/icons-material/ExploreOutlined';
import KeyboardArrowDownIcon from '@mui/icons-material/KeyboardArrowDown';
import KeyboardArrowUpIcon from '@mui/icons-material/KeyboardArrowUp';
import PeopleOutlinedIcon from '@mui/icons-material/PeopleOutlined';
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useAuth } from 'react-oidc-context';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, useHistory, useLocation, useParams } from 'react-router-dom';
import useFollowing from 'src/features/social/useFollowing';
import VideoComments from 'src/features/social/VideoComments';
import * as api from 'src/services/api/videos';
import { Video } from 'src/services/api/videos';
import { cssUrl } from 'src/features/videos/cssUrl';
import { brand } from 'src/theme';
import ReelSlide from './reels/ReelSlide';
import ReportVideoDialog from './ReportVideoDialog';

/** The next video is made ready this many videos ahead, and more are asked for when this few are left. */
const PRELOAD = 1;
const LOAD_MORE_WHEN_LEFT = 3;

const glass = {
   color: '#fff',
   backgroundColor: 'rgba(255,255,255,0.14)',
   backdropFilter: 'blur(12px)',
   border: '1px solid rgba(255,255,255,0.12)',
   '&:hover': { backgroundColor: 'rgba(255,255,255,0.26)' },
} as const;

/**
 * The videos one after the other, each as tall as the screen: swipe, scroll or press the arrow keys for the next. The
 * tabs choose between everybody's videos and those of the creators the person follows. `/reels/:videoId` starts with
 * that video, then goes on with the latest. Double tap a video to like it.
 */
export default function ReelsPage() {
   const { t } = useTranslation();
   const location = useLocation();
   const history = useHistory();
   const { videoId: startId } = useParams<{ videoId?: string }>();
   const auth = useAuth();
   const token = auth.user?.access_token;
   const scroller = useRef<HTMLDivElement>(null);
   const asked = useRef(new Set<string>());
   const onlyFollowed = new URLSearchParams(location.search).get('following') === '1';
   const bigScreen = useMediaQuery('(min-width:900px)');

   const [videos, setVideos] = useState<Video[] | null>(null);
   const [next, setNext] = useState<string | null>(null);
   const [failed, setFailed] = useState(false);
   const [loadingMore, setLoadingMore] = useState(false);
   const [links, setLinks] = useState<Record<string, string>>({});
   const [active, setActive] = useState(0);
   const [muted, setMuted] = useState(true);
   const [message, setMessage] = useState<string | null>(null);
   const [reporting, setReporting] = useState<string | null>(null);
   const [commenting, setCommenting] = useState<Video | null>(null);
   // the numbers the person changed by writing or deleting, over what the list said
   const [commentTotals, setCommentTotals] = useState<Record<string, number>>({});
   const followed = useFollowing(token);

   // the first screen: the video of the link (if there is one), then the latest
   useEffect(() => {
      // wait until it is known whether the person is signed in: what is locked depends on it
      if (auth.isLoading) return;
      let cancelled = false;
      setVideos(null);
      setFailed(false);
      setActive(0);
      asked.current = new Set();
      scroller.current?.scrollTo?.({ top: 0 });
      const start = startId ? api.fetchVideo(startId, token).catch(() => null) : Promise.resolve(null);
      Promise.all([api.fetchFeed(undefined, 12, onlyFollowed), start]).then(
         ([feed, first]) => {
            if (cancelled) return;
            if (first?.playbackUrl) {
               asked.current.add(first.videoId);
               setLinks((current) => ({ ...current, [first.videoId]: first.playbackUrl as string }));
            }
            const rest = feed.items.filter((x) => x.videoId !== first?.videoId);
            setVideos(first ? [first, ...rest] : rest);
            setNext(feed.next ?? null);
         },
         () => !cancelled && setFailed(true),
      );
      return () => {
         cancelled = true;
      };
   }, [auth.isLoading, token, startId, onlyFollowed]);

   const more = useCallback(async () => {
      if (!next || loadingMore) return;
      setLoadingMore(true);
      try {
         const feed = await api.fetchFeed(next, 12, onlyFollowed);
         setVideos((current) => {
            const known = new Set((current ?? []).map((x) => x.videoId));
            return [...(current ?? []), ...feed.items.filter((x) => !known.has(x.videoId))];
         });
         setNext(feed.next ?? null);
      } catch {
         // the ones that are loaded stay watchable
         setNext(null);
      }
      setLoadingMore(false);
   }, [next, loadingMore, onlyFollowed]);

   // the links that play are asked for the one in view and the next one, not for the whole list
   useEffect(() => {
      if (!videos) return;
      for (let i = active; i <= active + PRELOAD && i < videos.length; i++) {
         const video = videos[i];
         if (video.locked || asked.current.has(video.videoId)) continue;
         asked.current.add(video.videoId);
         api.fetchVideo(video.videoId, token).then(
            (full) =>
               full.playbackUrl && setLinks((current) => ({ ...current, [full.videoId]: full.playbackUrl as string })),
            () => asked.current.delete(video.videoId),
         );
      }
      if (next && videos.length - active <= LOAD_MORE_WHEN_LEFT) more();
   }, [videos, active, token, next, more]);

   // which one is in view
   const count = videos?.length ?? 0;
   useEffect(() => {
      const root = scroller.current;
      if (!root || count === 0 || typeof IntersectionObserver === 'undefined') return;
      const observer = new IntersectionObserver(
         (entries) =>
            entries.forEach((entry) => {
               if (entry.isIntersecting) setActive(Number((entry.target as HTMLElement).dataset.reel));
            }),
         { root, threshold: 0.6 },
      );
      root.querySelectorAll('[data-reel]').forEach((element) => observer.observe(element));
      return () => observer.disconnect();
   }, [count]);

   const go = useCallback((direction: 1 | -1) => {
      const root = scroller.current;
      root?.scrollBy?.({ top: direction * root.clientHeight, behavior: 'smooth' });
   }, []);

   // arrow keys for people with a keyboard, M for the sound
   useEffect(() => {
      const onKey = (event: KeyboardEvent) => {
         if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
            event.preventDefault();
            go(event.key === 'ArrowDown' ? 1 : -1);
         } else if (event.key === 'm' || event.key === 'M') {
            if (!(event.target instanceof HTMLTextAreaElement || event.target instanceof HTMLInputElement))
               setMuted((m) => !m);
         }
      };
      window.addEventListener('keydown', onKey);
      return () => window.removeEventListener('keydown', onKey);
   }, [go]);

   const signIn = useCallback(
      (returnTo?: string) => auth.signinRedirect({ state: { url: returnTo ?? location.pathname + location.search } }),
      [auth, location.pathname, location.search],
   );

   const share = async (video: Video) => {
      const link = api.videoLink(video.videoId);
      if (typeof navigator.share === 'function') {
         try {
            await navigator.share({ title: video.title, text: video.creator?.displayName ?? '', url: link });
            return;
         } catch (e) {
            // closing the share sheet is not a failure; anything else falls back to copying
            if ((e as Error)?.name === 'AbortError') return;
         }
      }
      try {
         await navigator.clipboard.writeText(link);
         setMessage(t('videos.link_copied'));
      } catch {
         window.prompt(t('sessions.copy_manually'), link);
      }
   };

   const follow = (video: Video) => (token ? video.creator && followed.toggle(video.creator.creatorId) : signIn());

   const report = (video: Video) => (token ? setReporting(video.videoId) : signIn());

   const choose = (following: boolean) => {
      if (following && !token) return signIn('/reels?following=1');
      history.replace(following ? '/reels?following=1' : '/reels');
   };

   const tab = (following: boolean, label: string, id: string) => {
      const selected = onlyFollowed === following;
      return (
         <Button
            id={id}
            onClick={() => choose(following)}
            aria-pressed={selected}
            sx={{
               color: '#fff',
               borderRadius: '999px',
               px: 2,
               py: 0.5,
               minWidth: 0,
               opacity: selected ? 1 : 0.65,
               backgroundColor: selected ? 'rgba(255,255,255,0.2)' : 'transparent',
               '&:hover': { backgroundColor: 'rgba(255,255,255,0.14)', opacity: 1 },
            }}
         >
            {label}
         </Button>
      );
   };

   const poster = videos?.[active]?.posterUrl;

   return (
      <Box
         id="reels-page"
         sx={{ position: 'fixed', inset: 0, backgroundColor: '#0a0706', color: '#fff', zIndex: 1200 }}
      >
         {/* the picture of the video in view, blurred, fills the sides of the frame */}
         <Box
            className="reels-backdrop"
            sx={{
               position: 'absolute',
               inset: -60,
               backgroundImage: poster ? cssUrl(poster) : brand.gradientSoft,
               backgroundSize: 'cover',
               backgroundPosition: 'center',
               filter: 'blur(60px) saturate(1.5) brightness(0.45)',
               transition: 'background-image 400ms ease',
            }}
         />

         <Box
            sx={{
               position: 'absolute',
               top: 0,
               left: 0,
               right: 0,
               zIndex: 6,
               p: 1.25,
               display: 'flex',
               alignItems: 'center',
               justifyContent: 'space-between',
               background: 'linear-gradient(180deg, rgba(0,0,0,0.5), transparent)',
               pointerEvents: 'none',
               '& > *': { pointerEvents: 'auto' },
            }}
         >
            <IconButton id="reels-close" component={RouterLink} to="/" aria-label={t('videos.reels_close')} sx={glass}>
               <CloseIcon />
            </IconButton>

            <Box
               id="reels-tabs"
               sx={{ display: 'flex', gap: 0.25, p: 0.5, borderRadius: '999px', ...glass, '&:hover': undefined }}
            >
               {tab(false, t('social.tab_for_you'), 'reels-tab-all')}
               {tab(true, t('social.tab_following'), 'reels-tab-following')}
            </Box>

            <Button
               id="reels-creators"
               component={RouterLink}
               to="/creators"
               aria-label={t('creators.nav_creators')}
               startIcon={<ExploreOutlinedIcon />}
               sx={{
                  ...glass,
                  borderRadius: '999px',
                  px: { xs: 1.25, sm: 2 },
                  minWidth: 0,
                  '& .MuiButton-startIcon': { mr: { xs: 0, sm: 1 }, ml: { xs: 0, sm: -0.5 } },
               }}
            >
               <Box component="span" sx={{ display: { xs: 'none', sm: 'inline' } }}>
                  {t('creators.nav_creators')}
               </Box>
            </Button>
         </Box>

         {failed && (
            <Alert severity="error" id="reels-failed" sx={{ position: 'relative', m: 8, mt: 10 }}>
               {t('videos.feed_failed')}
            </Alert>
         )}

         {videos === null && !failed && (
            <Box
               id="reels-loading"
               sx={{ position: 'relative', height: '100%', display: 'grid', placeItems: 'center' }}
            >
               <Skeleton
                  variant="rounded"
                  animation="wave"
                  sx={{
                     width: { xs: '100%', md: 'auto' },
                     height: { xs: '100%', md: 'min(calc(100dvh - 48px), 880px)' },
                     aspectRatio: { md: '9 / 16' },
                     borderRadius: { xs: 0, md: '26px' },
                     bgcolor: 'rgba(255,255,255,0.08)',
                  }}
               />
            </Box>
         )}

         {videos?.length === 0 && (
            <Box
               id="reels-empty"
               sx={{
                  position: 'relative',
                  height: '100%',
                  display: 'flex',
                  flexDirection: 'column',
                  alignItems: 'center',
                  justifyContent: 'center',
                  textAlign: 'center',
                  gap: 2,
                  px: 3,
               }}
            >
               <PeopleOutlinedIcon sx={{ fontSize: 64, opacity: 0.6 }} />
               <Typography sx={{ maxWidth: 360, opacity: 0.85 }}>
                  {onlyFollowed ? t('social.following_empty') : t('videos.reels_empty')}
               </Typography>
               {onlyFollowed && (
                  <Button
                     id="reels-find-creators"
                     variant="contained"
                     component={RouterLink}
                     to="/creators"
                     sx={{ background: brand.gradient, borderRadius: '999px', px: 3 }}
                  >
                     {t('creators.find')}
                  </Button>
               )}
            </Box>
         )}

         {videos && videos.length > 0 && (
            <Box
               ref={scroller}
               id="reels-scroller"
               sx={{
                  position: 'relative',
                  height: '100%',
                  overflowY: 'scroll',
                  scrollSnapType: 'y mandatory',
                  overscrollBehaviorY: 'contain',
                  scrollbarWidth: 'none',
                  '&::-webkit-scrollbar': { display: 'none' },
               }}
            >
               {videos.map((video, index) => (
                  <ReelSlide
                     key={video.videoId}
                     video={video}
                     index={index}
                     url={links[video.videoId]}
                     active={index === active}
                     muted={muted}
                     following={!!video.creator && followed.isFollowing(video.creator.creatorId)}
                     comments={commentTotals[video.videoId] ?? video.comments ?? 0}
                     signedIn={!!token}
                     onSignIn={signIn}
                     onFollow={() => follow(video)}
                     onComments={() => setCommenting(video)}
                     onToggleMute={() => setMuted((m) => !m)}
                     onShare={() => share(video)}
                     onReport={() => report(video)}
                  />
               ))}
               {loadingMore && (
                  <Box sx={{ height: 64, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                     <CircularProgress size={24} color="inherit" />
                  </Box>
               )}
            </Box>
         )}

         {videos && videos.length > 1 && bigScreen && (
            <Box
               className="reels-arrows"
               sx={{
                  position: 'absolute',
                  right: 24,
                  top: '50%',
                  transform: 'translateY(-50%)',
                  zIndex: 6,
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 1.5,
               }}
            >
               <IconButton
                  id="reels-up"
                  aria-label={t('videos.reels_previous')}
                  disabled={active === 0}
                  onClick={() => go(-1)}
                  sx={{ ...glass, '&.Mui-disabled': { opacity: 0.3, color: '#fff' } }}
               >
                  <KeyboardArrowUpIcon />
               </IconButton>
               <IconButton
                  id="reels-down"
                  aria-label={t('videos.reels_next')}
                  disabled={active >= videos.length - 1}
                  onClick={() => go(1)}
                  sx={{ ...glass, '&.Mui-disabled': { opacity: 0.3, color: '#fff' } }}
               >
                  <KeyboardArrowDownIcon />
               </IconButton>
            </Box>
         )}

         <Drawer
            anchor={bigScreen ? 'right' : 'bottom'}
            open={!!commenting}
            onClose={() => setCommenting(null)}
            sx={{ zIndex: 1300 }}
            slotProps={{
               paper: {
                  sx: bigScreen
                     ? { width: 420, maxWidth: '100%', p: 2.5 }
                     : {
                          maxHeight: '75vh',
                          borderRadius: '18px 18px 0 0',
                          p: 2,
                          mx: 'auto',
                          width: '100%',
                          maxWidth: 560,
                       },
               },
            }}
         >
            {commenting && (
               <Box id="reels-comments">
                  <Box sx={{ display: 'flex', justifyContent: 'flex-end' }}>
                     <IconButton
                        id="reels-comments-close"
                        size="small"
                        aria-label={t('social.comments_close')}
                        onClick={() => setCommenting(null)}
                     >
                        <CloseIcon fontSize="small" />
                     </IconButton>
                  </Box>
                  <VideoComments
                     key={commenting.videoId}
                     videoId={commenting.videoId}
                     signedIn={!!token}
                     onSignIn={() => signIn()}
                     locked={commenting.locked}
                     onTotal={(total) => setCommentTotals((current) => ({ ...current, [commenting.videoId]: total }))}
                  />
               </Box>
            )}
         </Drawer>

         <ReportVideoDialog
            key={reporting ?? 'none'}
            videoId={reporting ?? ''}
            open={!!reporting}
            onClose={() => setReporting(null)}
         />

         <Snackbar
            open={!!message}
            autoHideDuration={2200}
            onClose={() => setMessage(null)}
            message={message}
            sx={{ zIndex: 1400 }}
         />
      </Box>
   );
}
