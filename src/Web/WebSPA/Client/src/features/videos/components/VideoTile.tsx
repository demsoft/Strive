import { Box, Typography } from '@mui/material';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import PlayArrowRoundedIcon from '@mui/icons-material/PlayArrowRounded';
import React from 'react';
import { Link as RouterLink } from 'react-router-dom';
import { creatorPhotoUrl } from 'src/features/creator/photoUrl';
import HostAvatar from 'src/features/gbedu/ui/HostAvatar';
import { formatMoney } from 'src/features/sessions/money';
import { Video } from 'src/services/api/videos';
import { brand } from 'src/theme';
import { formatDuration } from '../videoFile';

/** A video in a list: the cover with its length, the title and who made it. It leads to the page where it plays. */
export default function VideoTile({ video }: { video: Video }) {
   const creator = video.creator;

   return (
      <Box
         className="video-tile"
         component={RouterLink}
         to={`/v/${video.videoId}`}
         sx={{
            display: 'flex',
            flexDirection: 'column',
            textDecoration: 'none',
            color: 'inherit',
            borderRadius: '18px',
            overflow: 'hidden',
            backgroundColor: brand.surface,
            border: `1px solid ${brand.border}`,
            transition: 'transform 120ms ease',
            '&:hover': { transform: 'translateY(-2px)' },
         }}
      >
         <Box
            sx={{
               position: 'relative',
               aspectRatio: '9 / 12',
               backgroundColor: 'rgba(255,255,255,0.06)',
               backgroundImage: video.posterUrl ? `url(${video.posterUrl})` : brand.gradient,
               backgroundSize: 'cover',
               backgroundPosition: 'center',
            }}
         >
            <Box
               sx={{
                  position: 'absolute',
                  inset: 0,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  background: 'linear-gradient(180deg, transparent 55%, rgba(0,0,0,0.55) 100%)',
               }}
            >
               {video.locked ? (
                  <LockOutlinedIcon className="video-lock" sx={{ fontSize: 48, color: '#fff', opacity: 0.9 }} />
               ) : (
                  <PlayArrowRoundedIcon sx={{ fontSize: 56, color: '#fff', opacity: 0.9 }} />
               )}
            </Box>
            {video.priceMinor > 0 && !video.isOwner && (
               <Box
                  className="video-price"
                  sx={{
                     position: 'absolute',
                     left: 8,
                     bottom: 8,
                     px: 0.75,
                     borderRadius: '6px',
                     background: video.locked ? brand.gradient : 'rgba(0,0,0,0.7)',
                     color: '#fff',
                     fontSize: 12,
                     fontWeight: 800,
                  }}
               >
                  {formatMoney(video.priceMinor, 'NGN')}
               </Box>
            )}
            {video.durationSeconds ? (
               <Box
                  className="video-duration"
                  sx={{
                     position: 'absolute',
                     right: 8,
                     bottom: 8,
                     px: 0.75,
                     borderRadius: '6px',
                     backgroundColor: 'rgba(0,0,0,0.7)',
                     color: '#fff',
                     fontSize: 12,
                     fontWeight: 700,
                  }}
               >
                  {formatDuration(video.durationSeconds)}
               </Box>
            ) : null}
         </Box>
         <Box sx={{ p: 1.5, display: 'flex', flexDirection: 'column', gap: 0.75 }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 700, lineHeight: 1.25 }} className="video-title">
               {video.title}
            </Typography>
            {creator && (
               <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                  <HostAvatar
                     name={creator.displayName}
                     size={22}
                     background={brand.gradient}
                     photoUrl={creatorPhotoUrl(creator.creatorId, creator.photoVersion, 22)}
                  />
                  <Typography variant="body2" color="text.secondary" noWrap>
                     {creator.displayName}
                  </Typography>
               </Box>
            )}
         </Box>
      </Box>
   );
}
