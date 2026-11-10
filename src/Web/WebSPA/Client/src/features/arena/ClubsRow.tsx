import { Box, Button, Typography } from '@mui/material';
import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router-dom';
import { creatorPhotoUrl } from 'src/features/creator/photoUrl';
import HostAvatar from 'src/features/gbedu/ui/HostAvatar';
import * as api from 'src/services/api/follows';
import { CreatorCard } from 'src/services/api/follows';
import { creatorPath } from 'src/services/api/sessions';
import { brand } from 'src/theme';

/** The clubs and leagues with the most followers, as a row of crests. Nothing is shown while there are none. */
export default function ClubsRow() {
   const { t } = useTranslation();
   const [cards, setCards] = useState<CreatorCard[]>([]);

   useEffect(() => {
      api.fetchCreatorCards({ sort: 'popular', limit: 12 }).then(
         (list) => setCards(list.items),
         () => setCards([]),
      );
   }, []);

   if (cards.length === 0) return null;

   return (
      <Box id="arena-clubs" sx={{ mb: 4 }}>
         <Box sx={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', mb: 1.5 }}>
            <Typography variant="h6" sx={{ fontWeight: 800 }}>
               {t('arena_home.clubs')}
            </Typography>
            <Button component={RouterLink} to="/creators" size="small" color="inherit">
               {t('arena_home.all_clubs')}
            </Button>
         </Box>
         <Box sx={{ display: 'flex', gap: 2, overflowX: 'auto', pb: 1 }}>
            {cards.map((card) => (
               <Box
                  key={card.creator.creatorId}
                  className="club-tile"
                  component={RouterLink}
                  to={`/u/${creatorPath(card.creator)}`}
                  sx={{
                     flex: '0 0 auto',
                     width: 120,
                     p: 1.5,
                     textAlign: 'center',
                     textDecoration: 'none',
                     color: 'inherit',
                     borderRadius: '20px',
                     backgroundColor: brand.surface,
                     border: `1px solid ${brand.border}`,
                  }}
               >
                  <HostAvatar
                     name={card.creator.displayName}
                     size={56}
                     background={brand.gradient}
                     photoUrl={creatorPhotoUrl(
                        card.creator.slug || card.creator.creatorId,
                        card.creator.photoVersion,
                        56,
                     )}
                  />
                  <Typography noWrap sx={{ fontWeight: 700, fontSize: 14, mt: 0.75 }}>
                     {card.creator.displayName}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                     {t('social.followers', { count: card.followers })}
                  </Typography>
               </Box>
            ))}
         </Box>
      </Box>
   );
}
