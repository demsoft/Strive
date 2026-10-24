import { Box, Button, Typography } from '@mui/material';
import { keyframes } from '@mui/material/styles';
import ChatBubbleOutlinedIcon from '@mui/icons-material/ChatBubbleOutlined';
import FavoriteIcon from '@mui/icons-material/Favorite';
import FlagOutlinedIcon from '@mui/icons-material/FlagOutlined';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import PlayArrowRoundedIcon from '@mui/icons-material/PlayArrowRounded';
import ShareOutlinedIcon from '@mui/icons-material/ShareOutlined';
import VolumeOffIcon from '@mui/icons-material/VolumeOff';
import VolumeUpIcon from '@mui/icons-material/VolumeUp';
import React, { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router-dom';
import { creatorPhotoUrl } from 'src/features/creator/photoUrl';
import HostAvatar from 'src/features/gbedu/ui/HostAvatar';
import { formatMoney } from 'src/features/sessions/money';
import FollowButton from 'src/features/social/FollowButton';
import LikeButton from 'src/features/social/LikeButton';
import useVideoLike from 'src/features/social/useVideoLike';
import { Video } from 'src/services/api/videos';
import { cssUrl } from 'src/features/videos/cssUrl';
import { brand } from 'src/theme';
import RailButton from './RailButton';

const heartPop = keyframes`
   0% { transform: translate(-50%, -50%) scale(0.3); opacity: 0; }
   20% { transform: translate(-50%, -50%) scale(1.3); opacity: 1; }
   55% { transform: translate(-50%, -50%) scale(1); opacity: 1; }
   100% { transform: translate(-50%, -64%) scale(1.15); opacity: 0; }
`;

/** Two taps this close are a double tap */
const DOUBLE_TAP_MS = 300;

export type ReelSlideProps = {
   video: Video;
   index: number;
   /** the link that plays it, once known */
   url?: string;
   active: boolean;
   muted: boolean;
   following: boolean;
   comments: number;
   signedIn: boolean;
   onSignIn: () => void;
   onFollow: () => void;
   onComments: () => void;
   onToggleMute: () => void;
   onShare: () => void;
   onReport: () => void;
};

/**
 * One video as tall as the screen. On a phone it fills the screen with the buttons on top of it; on a big screen it is
 * a phone-shaped frame with the buttons beside it. It plays while it is in view and is a picture otherwise.
 */
export default function ReelSlide({
   video,
   index,
   url,
   active,
   muted,
   following,
   comments,
   signedIn,
   onSignIn,
   onFollow,
   onComments,
   onToggleMute,
   onShare,
   onReport,
}: ReelSlideProps) {
   const { t } = useTranslation();
   const player = useRef<HTMLVideoElement>(null);
   const progress = useRef<HTMLDivElement>(null);
   const lastTap = useRef(0);
   const tapTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
   const [paused, setPaused] = useState(false);
   const [burst, setBurst] = useState(0);
   const creator = video.creator;
   const like = useVideoLike(video.videoId, video, signedIn, onSignIn);
   // a wide video is shown whole, a tall one fills the frame
   const wide = (video.width ?? 0) > (video.height ?? 0);

   // playing is the business of the one in view; the others do not even have a player
   useEffect(() => {
      const element = player.current;
      if (!element || !active) return;
      // the browser may refuse to start without a tap, then the person taps
      Promise.resolve(element.play?.()).catch(() => undefined);
   }, [active, url]);

   useEffect(
      () => () => {
         if (tapTimer.current) clearTimeout(tapTimer.current);
      },
      [],
   );

   const togglePlay = () => {
      const element = player.current;
      if (!element) return;
      if (element.paused) Promise.resolve(element.play?.()).catch(() => undefined);
      else element.pause?.();
   };

   // one tap pauses or plays, two taps like (and show the heart)
   const tap = () => {
      const now = Date.now();
      if (tapTimer.current && now - lastTap.current < DOUBLE_TAP_MS) {
         clearTimeout(tapTimer.current);
         tapTimer.current = undefined;
         lastTap.current = 0;
         if (!signedIn) return onSignIn();
         if (!like.liked) like.toggle();
         setBurst((b) => b + 1);
         return;
      }
      lastTap.current = now;
      tapTimer.current = setTimeout(() => {
         tapTimer.current = undefined;
         togglePlay();
      }, DOUBLE_TAP_MS - 40);
   };

   const glass = {
      color: '#fff',
      backgroundColor: 'rgba(255,255,255,0.14)',
      backdropFilter: 'blur(10px)',
      border: '1px solid rgba(255,255,255,0.12)',
   } as const;

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
            alignItems: 'center',
            justifyContent: 'center',
            gap: 2,
         }}
      >
         <Box
            className="reel-frame"
            sx={{
               position: 'relative',
               overflow: 'hidden',
               backgroundColor: '#000',
               backgroundImage: video.posterUrl ? cssUrl(video.posterUrl) : brand.gradient,
               backgroundSize: wide ? 'contain' : 'cover',
               backgroundRepeat: 'no-repeat',
               backgroundPosition: 'center',
               width: { xs: '100%', md: 'auto' },
               height: { xs: '100%', md: 'min(calc(100dvh - 48px), 880px)' },
               maxWidth: '100%',
               aspectRatio: { md: '9 / 16' },
               borderRadius: { xs: 0, md: '26px' },
               boxShadow: { md: '0 30px 80px -20px rgba(0,0,0,0.8)' },
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
                  onClick={tap}
                  onPlay={() => setPaused(false)}
                  onPause={() => setPaused(true)}
                  onTimeUpdate={(e) => {
                     const element = e.currentTarget;
                     if (progress.current && element.duration)
                        progress.current.style.width = `${(element.currentTime / element.duration) * 100}%`;
                  }}
                  style={{
                     position: 'absolute',
                     inset: 0,
                     width: '100%',
                     height: '100%',
                     objectFit: wide ? 'contain' : 'cover',
                     cursor: 'pointer',
                  }}
               />
            )}

            {paused && active && (
               <Box
                  className="reel-paused"
                  sx={{
                     position: 'absolute',
                     top: '50%',
                     left: '50%',
                     transform: 'translate(-50%, -50%)',
                     width: 76,
                     height: 76,
                     borderRadius: '50%',
                     display: 'grid',
                     placeItems: 'center',
                     pointerEvents: 'none',
                     ...glass,
                  }}
               >
                  <PlayArrowRoundedIcon sx={{ fontSize: 48 }} />
               </Box>
            )}

            {burst > 0 && (
               <FavoriteIcon
                  key={burst}
                  className="reel-heart"
                  sx={{
                     position: 'absolute',
                     top: '50%',
                     left: '50%',
                     fontSize: 120,
                     color: '#ff4d6d',
                     pointerEvents: 'none',
                     filter: 'drop-shadow(0 6px 18px rgba(0,0,0,0.5))',
                     animation: `${heartPop} 800ms ease-out forwards`,
                  }}
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
                     backdropFilter: 'blur(22px)',
                     background: 'rgba(10,6,4,0.62)',
                     color: '#fff',
                     textAlign: 'center',
                     px: 4,
                  }}
               >
                  <Box
                     sx={{
                        width: 76,
                        height: 76,
                        borderRadius: '50%',
                        display: 'grid',
                        placeItems: 'center',
                        ...glass,
                     }}
                  >
                     <LockOutlinedIcon sx={{ fontSize: 36 }} />
                  </Box>
                  <Typography variant="h5" sx={{ fontWeight: 800 }}>
                     {t('videos.locked_title')}
                  </Typography>
                  <Typography sx={{ opacity: 0.8 }}>{formatMoney(video.priceMinor, 'NGN')}</Typography>
                  <Button
                     className="reel-unlock"
                     variant="contained"
                     size="large"
                     component={RouterLink}
                     to={`/v/${video.videoId}`}
                     sx={{ background: brand.gradient, borderRadius: '999px', px: 4 }}
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
                  pb: 3,
                  pr: { xs: 9, md: 2 },
                  color: '#fff',
                  pointerEvents: 'none',
                  background: 'linear-gradient(180deg, transparent 0%, rgba(0,0,0,0.78) 100%)',
               }}
            >
               {creator && (
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25, mb: 1, pointerEvents: 'auto' }}>
                     <Box
                        className="reel-creator"
                        component={RouterLink}
                        to={`/u/${creator.slug || creator.creatorId}`}
                        sx={{
                           display: 'inline-flex',
                           alignItems: 'center',
                           gap: 1,
                           color: 'inherit',
                           textDecoration: 'none',
                           minWidth: 0,
                        }}
                     >
                        <HostAvatar
                           name={creator.displayName}
                           size={40}
                           background={brand.gradient}
                           photoUrl={creatorPhotoUrl(creator.creatorId, creator.photoVersion, 40)}
                        />
                        <Typography sx={{ fontWeight: 800 }} noWrap>
                           {creator.displayName}
                        </Typography>
                     </Box>
                     {!video.isOwner && (
                        <FollowButton className="reel-follow" size="small" following={following} onClick={onFollow} />
                     )}
                  </Box>
               )}
               <Typography className="reel-title" sx={{ fontWeight: 700, lineHeight: 1.3 }}>
                  {video.title}
               </Typography>
               {video.description && (
                  <Typography
                     className="reel-description"
                     variant="body2"
                     sx={{
                        opacity: 0.88,
                        mt: 0.25,
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
               className="reel-progress"
               sx={{
                  position: 'absolute',
                  left: 0,
                  right: 0,
                  bottom: 0,
                  height: 3,
                  backgroundColor: 'rgba(255,255,255,0.18)',
               }}
            >
               <Box ref={progress} sx={{ height: '100%', width: 0, background: brand.gradient }} />
            </Box>
         </Box>

         <Box
            className="reel-rail"
            sx={{
               position: { xs: 'absolute', md: 'relative' },
               right: { xs: 8, md: 'auto' },
               bottom: { xs: 28, md: 'auto' },
               alignSelf: { md: 'flex-end' },
               mb: { md: 3 },
               display: 'flex',
               flexDirection: 'column',
               alignItems: 'center',
               gap: 1.75,
            }}
         >
            <LikeButton compact liked={like.liked} count={like.likes} onClick={like.toggle} />
            <RailButton
               className="reel-comments"
               label={t('social.comments_title')}
               icon={<ChatBubbleOutlinedIcon />}
               caption={comments}
               onClick={onComments}
            />
            <RailButton
               className="reel-share"
               label={t('videos.share')}
               icon={<ShareOutlinedIcon />}
               caption={t('videos.share_short')}
               onClick={onShare}
            />
            <RailButton
               className="reel-mute"
               label={muted ? t('videos.reels_unmute') : t('videos.reels_mute')}
               icon={muted ? <VolumeOffIcon /> : <VolumeUpIcon />}
               onClick={onToggleMute}
            />
            {!video.isOwner && (
               <RailButton
                  className="reel-report"
                  label={t('videos.report')}
                  icon={<FlagOutlinedIcon />}
                  onClick={onReport}
               />
            )}
         </Box>
      </Box>
   );
}
