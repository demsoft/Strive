import { Box, Button, Typography } from '@mui/material';
import React, { useEffect, useState } from 'react';
import { useAuth } from 'react-oidc-context';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, useLocation } from 'react-router-dom';
import { creatorPhotoUrl } from 'src/features/creator/photoUrl';
import HostAvatar from 'src/features/gbedu/ui/HostAvatar';
import FollowButton from 'src/features/social/FollowButton';
import * as api from 'src/services/api/follows';
import { CreatorCard } from 'src/services/api/follows';
import { creatorPath } from 'src/services/api/sessions';
import { brand } from 'src/theme';
import { compactCount } from './count';

/** On the home page: a row of popular creators to follow, and the way to all of them. Nothing is shown while there are none. */
export default function DiscoverCreators() {
   const { t } = useTranslation();
   const auth = useAuth();
   const token = auth.user?.access_token;
   const location = useLocation();
   const [cards, setCards] = useState<CreatorCard[] | null>(null);

   useEffect(() => {
      if (auth.isLoading) return;
      let cancelled = false;
      api.fetchCreatorCards({ sort: 'popular', limit: 12 }).then(
         (list) => !cancelled && setCards(list.items),
         () => !cancelled && setCards([]),
      );
      return () => {
         cancelled = true;
      };
   }, [auth.isLoading, token]);

   const toggle = async (card: CreatorCard) => {
      if (!token) return auth.signinRedirect({ state: { url: location.pathname } });

      const id = card.creator.creatorId;
      const set = (following: boolean) =>
         setCards((current) =>
            (current ?? []).map((x) =>
               x.creator.creatorId === id
                  ? {
                       ...x,
                       following,
                       followers: Math.max(0, x.followers + (following === x.following ? 0 : following ? 1 : -1)),
                    }
                  : x,
            ),
         );
      const was = card.following;
      set(!was);
      try {
         await (was ? api.unfollowCreator(id) : api.followCreator(id));
      } catch {
         set(was);
      }
   };

   if (!cards || cards.length === 0) return null;

   return (
      <Box id="discover-creators" sx={{ maxWidth: 1180, width: '100%', mx: 'auto', py: 4 }}>
         <Box sx={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 2, mb: 2 }}>
            <Box>
               <Typography variant="h4" sx={{ fontWeight: 800 }}>
                  {t('creators.strip_title')}
               </Typography>
               <Typography color="text.secondary">{t('creators.strip_subtitle')}</Typography>
            </Box>
            <Button id="discover-creators-all" component={RouterLink} to="/creators">
               {t('creators.see_all')}
            </Button>
         </Box>

         <Box
            sx={{
               display: 'flex',
               gap: 2,
               overflowX: 'auto',
               pb: 1.5,
               scrollSnapType: 'x proximity',
               scrollbarWidth: 'thin',
            }}
         >
            {cards.map((card) => (
               <Box
                  key={card.creator.creatorId}
                  className="creator-chip"
                  sx={{
                     position: 'relative',
                     flex: '0 0 176px',
                     scrollSnapAlign: 'start',
                     p: 2,
                     display: 'flex',
                     flexDirection: 'column',
                     alignItems: 'center',
                     textAlign: 'center',
                     borderRadius: '20px',
                     backgroundColor: brand.surface,
                     border: `1px solid ${brand.border}`,
                     transition: 'transform 140ms ease, border-color 140ms ease',
                     '&:hover': { transform: 'translateY(-2px)', borderColor: 'rgba(255,255,255,0.16)' },
                  }}
               >
                  <Box
                     component={RouterLink}
                     to={`/u/${creatorPath(card.creator)}`}
                     className="creator-chip-link"
                     aria-label={card.creator.displayName}
                     sx={{ position: 'absolute', inset: 0, zIndex: 1, borderRadius: '20px' }}
                  />
                  <Box sx={{ borderRadius: '50%', p: '3px', background: brand.gradient, lineHeight: 0 }}>
                     <Box sx={{ borderRadius: '50%', border: `3px solid ${brand.surface}`, lineHeight: 0 }}>
                        <HostAvatar
                           name={card.creator.displayName}
                           size={72}
                           background={brand.gradient}
                           photoUrl={creatorPhotoUrl(card.creator.creatorId, card.creator.photoVersion, 72)}
                        />
                     </Box>
                  </Box>
                  <Typography sx={{ fontWeight: 700, mt: 1.25, width: '100%' }} noWrap>
                     {card.creator.displayName}
                  </Typography>
                  <Typography variant="caption" color="text.secondary" sx={{ mb: 1.25 }}>
                     {compactCount(card.followers)} {t('creators.follower_word', { count: card.followers })}
                  </Typography>
                  <Box sx={{ position: 'relative', zIndex: 2, width: '100%' }}>
                     {!card.isYou && (
                        <FollowButton
                           className="creator-chip-follow"
                           size="small"
                           fullWidth
                           following={card.following}
                           onClick={() => toggle(card)}
                        />
                     )}
                  </Box>
               </Box>
            ))}
         </Box>
      </Box>
   );
}
