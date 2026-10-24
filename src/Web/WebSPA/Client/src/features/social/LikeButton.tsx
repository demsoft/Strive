import { Box, Button, IconButton, Typography } from '@mui/material';
import FavoriteIcon from '@mui/icons-material/Favorite';
import FavoriteBorderIcon from '@mui/icons-material/FavoriteBorder';
import React from 'react';
import { useTranslation } from 'react-i18next';

type Props = {
   liked: boolean;
   count: number;
   onClick: () => void;
   /** a round button with the number below it, for the side of a full screen video */
   compact?: boolean;
};

/** The heart with the number of likes. It is filled when the person likes the video. */
export default function LikeButton({ liked, count, onClick, compact }: Props) {
   const { t } = useTranslation();
   const label = liked ? t('social.unlike') : t('social.like');
   const icon = liked ? <FavoriteIcon sx={{ color: '#ff4d6d' }} /> : <FavoriteBorderIcon />;

   if (compact)
      return (
         <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 0.25 }}>
            <IconButton
               className="like-button"
               aria-label={label}
               aria-pressed={liked}
               onClick={onClick}
               sx={{
                  width: 48,
                  height: 48,
                  color: '#fff',
                  backgroundColor: 'rgba(255,255,255,0.14)',
                  backdropFilter: 'blur(10px)',
                  border: '1px solid rgba(255,255,255,0.12)',
                  transition: 'transform 120ms ease, background-color 120ms ease',
                  '&:hover': { backgroundColor: 'rgba(255,255,255,0.26)' },
                  '&:active': { transform: 'scale(0.88)' },
               }}
            >
               {icon}
            </IconButton>
            <Typography
               className="like-count"
               variant="caption"
               sx={{ color: '#fff', fontWeight: 700, textShadow: '0 1px 4px rgba(0,0,0,0.6)' }}
            >
               {count}
            </Typography>
         </Box>
      );

   return (
      <Button
         className="like-button"
         variant="outlined"
         color="inherit"
         aria-label={label}
         aria-pressed={liked}
         startIcon={icon}
         onClick={onClick}
      >
         <span className="like-count">{count}</span>
      </Button>
   );
}
