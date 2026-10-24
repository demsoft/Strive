import { Box, Typography } from '@mui/material';
import PlayCircleOutlinedIcon from '@mui/icons-material/PlayCircleOutlined';
import EventOutlinedIcon from '@mui/icons-material/EventOutlined';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router-dom';
import { creatorPhotoUrl } from 'src/features/creator/photoUrl';
import HostAvatar from 'src/features/gbedu/ui/HostAvatar';
import FollowButton from 'src/features/social/FollowButton';
import { CreatorCard as Card } from 'src/services/api/follows';
import { creatorPath } from 'src/services/api/sessions';
import { cssUrl } from 'src/features/videos/cssUrl';
import { brand } from 'src/theme';
import { compactCount } from './count';

type Props = { card: Card; onFollow: () => void };

/** A creator to look at and follow: the cover of their latest video, who they are, what they have, a Follow button. */
export default function CreatorCard({ card, onFollow }: Props) {
   const { t } = useTranslation();
   const { creator } = card;

   const badge = {
      display: 'inline-flex',
      alignItems: 'center',
      gap: 0.5,
      px: 1,
      py: 0.25,
      borderRadius: '999px',
      fontSize: 12,
      fontWeight: 700,
      color: '#fff',
      backgroundColor: 'rgba(0,0,0,0.55)',
      backdropFilter: 'blur(6px)',
   } as const;

   return (
      <Box
         className="creator-card"
         sx={{
            position: 'relative',
            display: 'flex',
            flexDirection: 'column',
            borderRadius: '22px',
            overflow: 'hidden',
            backgroundColor: brand.surface,
            border: `1px solid ${brand.border}`,
            transition: 'transform 160ms ease, box-shadow 160ms ease, border-color 160ms ease',
            '&:hover': {
               transform: 'translateY(-3px)',
               boxShadow: `0 14px 36px -16px ${brand.accentShadow}`,
               borderColor: 'rgba(255,255,255,0.16)',
            },
         }}
      >
         <Box
            component={RouterLink}
            to={`/u/${creatorPath(creator)}`}
            className="creator-card-link"
            aria-label={creator.displayName}
            sx={{ position: 'absolute', inset: 0, zIndex: 1 }}
         />

         <Box
            className="creator-card-cover"
            sx={{
               position: 'relative',
               aspectRatio: '16 / 10',
               backgroundColor: 'rgba(255,255,255,0.05)',
               backgroundImage: card.coverUrl
                  ? `linear-gradient(180deg, rgba(0,0,0,0) 45%, rgba(0,0,0,0.38) 100%), ${cssUrl(card.coverUrl)}`
                  : brand.gradientSoft,
               backgroundSize: 'cover',
               backgroundPosition: 'center',
            }}
         >
            <Box sx={{ position: 'absolute', top: 10, left: 10, display: 'flex', gap: 0.75, flexWrap: 'wrap' }}>
               {card.videos > 0 && (
                  <Box component="span" className="creator-card-videos" sx={badge}>
                     <PlayCircleOutlinedIcon sx={{ fontSize: 14 }} />
                     {t('creators.videos_count', { count: card.videos })}
                  </Box>
               )}
               {card.sessions > 0 && (
                  <Box component="span" className="creator-card-sessions" sx={badge}>
                     <EventOutlinedIcon sx={{ fontSize: 14 }} />
                     {t('creators.sessions_count', { count: card.sessions })}
                  </Box>
               )}
            </Box>
         </Box>

         <Box sx={{ px: 2, pb: 2, display: 'flex', flexDirection: 'column', flex: 1 }}>
            <Box sx={{ mt: '-34px', mb: 1, position: 'relative', alignSelf: 'flex-start' }}>
               <Box sx={{ borderRadius: '50%', border: `4px solid ${brand.surface}`, lineHeight: 0 }}>
                  <HostAvatar
                     name={creator.displayName}
                     size={68}
                     background={brand.gradient}
                     photoUrl={creatorPhotoUrl(creator.creatorId, creator.photoVersion, 68)}
                  />
               </Box>
            </Box>

            <Typography className="creator-card-name" sx={{ fontWeight: 800, fontSize: 18, lineHeight: 1.2 }} noWrap>
               {creator.displayName}
            </Typography>
            <Typography className="creator-card-followers" variant="body2" color="text.secondary" sx={{ mb: 0.75 }}>
               {compactCount(card.followers)} {t('creators.follower_word', { count: card.followers })}
            </Typography>
            <Typography
               className="creator-card-bio"
               variant="body2"
               sx={{
                  color: 'text.secondary',
                  minHeight: 40,
                  display: '-webkit-box',
                  WebkitLineClamp: 2,
                  WebkitBoxOrient: 'vertical',
                  overflow: 'hidden',
                  mb: 1.5,
               }}
            >
               {creator.bio || ' '}
            </Typography>

            <Box sx={{ mt: 'auto', position: 'relative', zIndex: 2 }}>
               {card.isYou ? (
                  <Typography className="creator-card-you" variant="body2" color="text.secondary" sx={{ py: 0.75 }}>
                     {t('creators.this_is_you')}
                  </Typography>
               ) : (
                  <FollowButton
                     className="creator-card-follow"
                     fullWidth
                     following={card.following}
                     onClick={onFollow}
                  />
               )}
            </Box>
         </Box>
      </Box>
   );
}
