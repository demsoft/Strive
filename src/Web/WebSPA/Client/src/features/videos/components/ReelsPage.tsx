import { Alert, Box, Button, CircularProgress, IconButton, Typography } from '@mui/material';
import CloseIcon from '@mui/icons-material/Close';
import FlagOutlinedIcon from '@mui/icons-material/FlagOutlined';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import ShareIcon from '@mui/icons-material/Share';
import VolumeOffIcon from '@mui/icons-material/VolumeOff';
import VolumeUpIcon from '@mui/icons-material/VolumeUp';
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useAuth } from 'react-oidc-context';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, useLocation, useParams } from 'react-router-dom';
import { creatorPhotoUrl } from 'src/features/creator/photoUrl';
import HostAvatar from 'src/features/gbedu/ui/HostAvatar';
import { formatMoney } from 'src/features/sessions/money';
import * as api from 'src/services/api/videos';
import { Video } from 'src/services/api/videos';
import { brand } from 'src/theme';
import ReportVideoDialog from './ReportVideoDialog';

/** The next video is made ready this many videos ahead, and more are asked for when this few are left. */
const PRELOAD = 1;
const LOAD_MORE_WHEN_LEFT = 3;

type ReelProps = {
   video: Video;
   index: number;
   /** the link that plays it, once known */
   url?: string;
   active: boolean;
   muted: boolean;
   copied: boolean;
   onToggleMute: () => void;
   onShare: () => void;
   onReport: () => void;
};

/** One video as tall as the screen: it plays while it is the one in view, and is a picture otherwise. */
function Reel({ video, index, url, active, muted, copied, onToggleMute, onShare, onReport }: ReelProps) {
   const { t } = useTranslation();
   const player = useRef<HTMLVideoElement>(null);
   const creator = video.creator;

   // playing is the business of the one in view; the others do not even have a player
   useEffect(() => {
      const element = player.current;
      if (!element || !active) return;
      // the browser may refuse to start without a tap, then the person taps
      Promise.resolve(element.play?.()).catch(() => undefined);
   }, [active, url]);

   const tapPlayer = () => {
      const element = player.current;
      if (!element) return;
      if (element.paused) Promise.resolve(element.play?.()).catch(() => undefined);
      else element.pause?.();
   };

   return (
      <Box
         className="reel"
         data-reel={index}
         sx={{
            position: 'relative',
            height: '100%',
            scrollSnapAlign: 'start',
            scrollSnapStop: 'always',
            display: 'flex',
            justifyContent: 'center',
            backgroundColor: '#000',
         }}
      >
         <Box
            sx={{
               position: 'relative',
               width: '100%',
               maxWidth: 'calc(100dvh * 9 / 16)',
               height: '100%',
               backgroundColor: '#000',
               backgroundImage: video.posterUrl ? `url(${video.posterUrl})` : brand.gradient,
               backgroundSize: 'contain',
               backgroundRepeat: 'no-repeat',
               backgroundPosition: 'center',
               overflow: 'hidden',
            }}
         >
            {active && url && !video.locked && (
               <video
                  ref={player}
                  className="reel-player"
                  src={url}
                  poster={video.posterUrl ?? undefined}
                  autoPlay
                  loop
                  muted={muted}
                  playsInline
                  onClick={tapPlayer}
                  style={{ position: 'absolute', inset: 0, width: '100%', height: '100%', objectFit: 'contain' }}
               />
            )}

            {video.locked && (
               <Box
                  className="reel-locked"
                  sx={{
                     position: 'absolute',
                     inset: 0,
                     display: 'flex',
                     flexDirection: 'column',
                     alignItems: 'center',
                     justifyContent: 'center',
                     gap: 1.5,
                     background: 'rgba(0,0,0,0.55)',
                     color: '#fff',
                     textAlign: 'center',
                     px: 3,
                  }}
               >
                  <LockOutlinedIcon sx={{ fontSize: 48 }} />
                  <Typography variant="h6" sx={{ fontWeight: 800 }}>
                     {t('videos.locked_title')}
                  </Typography>
                  <Button
                     className="reel-unlock"
                     variant="contained"
                     component={RouterLink}
                     to={`/v/${video.videoId}`}
                     sx={{ background: brand.gradient }}
                  >
                     {t('videos.reels_unlock', { price: formatMoney(video.priceMinor, 'NGN') })}
                  </Button>
               </Box>
            )}

            <Box
               sx={{
                  position: 'absolute',
                  left: 0,
                  right: 0,
                  bottom: 0,
                  p: 2,
                  pr: 8,
                  pb: 3,
                  color: '#fff',
                  pointerEvents: 'none',
                  background: 'linear-gradient(180deg, transparent 0%, rgba(0,0,0,0.7) 100%)',
               }}
            >
               {creator && (
                  <Box
                     className="reel-creator"
                     component={RouterLink}
                     to={`/u/${creator.slug || creator.creatorId}`}
                     sx={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: 1,
                        mb: 1,
                        color: 'inherit',
                        textDecoration: 'none',
                        pointerEvents: 'auto',
                     }}
                  >
                     <HostAvatar
                        name={creator.displayName}
                        size={36}
                        background={brand.gradient}
                        photoUrl={creatorPhotoUrl(creator.creatorId, creator.photoVersion, 36)}
                     />
                     <Typography sx={{ fontWeight: 700 }}>{creator.displayName}</Typography>
                  </Box>
               )}
               <Typography className="reel-title" sx={{ fontWeight: 700 }}>
                  {video.title}
               </Typography>
               {video.description && (
                  <Typography
                     className="reel-description"
                     variant="body2"
                     sx={{
                        opacity: 0.9,
                        display: '-webkit-box',
                        WebkitLineClamp: 2,
                        WebkitBoxOrient: 'vertical',
                        overflow: 'hidden',
                     }}
                  >
                     {video.description}
                  </Typography>
               )}
            </Box>

            <Box
               sx={{
                  position: 'absolute',
                  right: 8,
                  bottom: 24,
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 1,
                  alignItems: 'center',
               }}
            >
               <IconButton
                  className="reel-mute"
                  aria-label={muted ? t('videos.reels_unmute') : t('videos.reels_mute')}
                  onClick={onToggleMute}
                  sx={{ color: '#fff', backgroundColor: 'rgba(0,0,0,0.4)' }}
               >
                  {muted ? <VolumeOffIcon /> : <VolumeUpIcon />}
               </IconButton>
               <IconButton
                  className="reel-share"
                  aria-label={copied ? t('sessions.copied') : t('videos.share')}
                  onClick={onShare}
                  sx={{ color: '#fff', backgroundColor: 'rgba(0,0,0,0.4)' }}
               >
                  <ShareIcon />
               </IconButton>
               {!video.isOwner && (
                  <IconButton
                     className="reel-report"
                     aria-label={t('videos.report')}
                     onClick={onReport}
                     sx={{ color: '#fff', backgroundColor: 'rgba(0,0,0,0.4)' }}
                  >
                     <FlagOutlinedIcon />
                  </IconButton>
               )}
            </Box>
         </Box>
      </Box>
   );
}

/**
 * The videos one after the other, each as tall as the screen: swipe (or scroll, or press the arrow keys) to the next.
 * `/reels/:videoId` starts with that video, then goes on with the latest.
 */
export default function ReelsPage() {
   const { t } = useTranslation();
   const { videoId: startId } = useParams<{ videoId?: string }>();
   const auth = useAuth();
   const token = auth.user?.access_token;
   const location = useLocation();
   const scroller = useRef<HTMLDivElement>(null);
   const asked = useRef(new Set<string>());

   const [videos, setVideos] = useState<Video[] | null>(null);
   const [next, setNext] = useState<string | null>(null);
   const [failed, setFailed] = useState(false);
   const [loadingMore, setLoadingMore] = useState(false);
   const [links, setLinks] = useState<Record<string, string>>({});
   const [active, setActive] = useState(0);
   const [muted, setMuted] = useState(true);
   const [copiedId, setCopiedId] = useState<string | null>(null);
   const [reporting, setReporting] = useState<string | null>(null);

   // the first screen: the video of the link (if there is one), then the latest
   useEffect(() => {
      // wait until it is known whether the person is signed in: what is locked depends on it
      if (auth.isLoading) return;
      let cancelled = false;
      const start = startId ? api.fetchVideo(startId, token).catch(() => null) : Promise.resolve(null);
      Promise.all([api.fetchFeed(), start]).then(
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
   }, [auth.isLoading, token, startId]);

   const more = useCallback(async () => {
      if (!next || loadingMore) return;
      setLoadingMore(true);
      try {
         const feed = await api.fetchFeed(next);
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
   }, [next, loadingMore]);

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

   // arrow keys for people with a keyboard
   useEffect(() => {
      const onKey = (event: KeyboardEvent) => {
         const root = scroller.current;
         if (!root || (event.key !== 'ArrowDown' && event.key !== 'ArrowUp')) return;
         event.preventDefault();
         root.scrollBy({ top: (event.key === 'ArrowDown' ? 1 : -1) * root.clientHeight, behavior: 'smooth' });
      };
      window.addEventListener('keydown', onKey);
      return () => window.removeEventListener('keydown', onKey);
   }, []);

   const share = async (video: Video) => {
      const link = api.videoLink(video.videoId);
      try {
         await navigator.clipboard.writeText(link);
         setCopiedId(video.videoId);
      } catch {
         window.prompt(t('sessions.copy_manually'), link);
      }
   };

   const report = (video: Video) =>
      token ? setReporting(video.videoId) : auth.signinRedirect({ state: { url: location.pathname } });

   return (
      <Box id="reels-page" sx={{ position: 'fixed', inset: 0, backgroundColor: '#000', color: '#fff', zIndex: 1200 }}>
         <IconButton
            id="reels-close"
            component={RouterLink}
            to="/"
            aria-label={t('videos.reels_close')}
            sx={{
               position: 'absolute',
               top: 8,
               left: 8,
               zIndex: 2,
               color: '#fff',
               backgroundColor: 'rgba(0,0,0,0.4)',
            }}
         >
            <CloseIcon />
         </IconButton>

         {failed && (
            <Alert severity="error" id="reels-failed" sx={{ m: 8, mt: 10 }}>
               {t('videos.feed_failed')}
            </Alert>
         )}
         {videos === null && !failed && (
            <Box sx={{ height: '100%', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
               <CircularProgress color="inherit" />
            </Box>
         )}
         {videos?.length === 0 && (
            <Box id="reels-empty" sx={{ height: '100%', display: 'grid', placeItems: 'center', textAlign: 'center' }}>
               <Typography>{t('videos.reels_empty')}</Typography>
            </Box>
         )}

         {videos && videos.length > 0 && (
            <Box
               ref={scroller}
               id="reels-scroller"
               sx={{
                  height: '100%',
                  overflowY: 'scroll',
                  scrollSnapType: 'y mandatory',
                  overscrollBehaviorY: 'contain',
                  scrollbarWidth: 'none',
                  '&::-webkit-scrollbar': { display: 'none' },
               }}
            >
               {videos.map((video, index) => (
                  <Reel
                     key={video.videoId}
                     video={video}
                     index={index}
                     url={links[video.videoId]}
                     active={index === active}
                     muted={muted}
                     copied={copiedId === video.videoId}
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

         <ReportVideoDialog
            key={reporting ?? 'none'}
            videoId={reporting ?? ''}
            open={!!reporting}
            onClose={() => setReporting(null)}
         />
      </Box>
   );
}
