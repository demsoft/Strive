import { Box, IconButton, Typography } from '@mui/material';
import React from 'react';

type Props = {
   icon: React.ReactNode;
   label: string;
   /** the number or word under the button */
   caption?: string | number;
   onClick: () => void;
   className: string;
   pressed?: boolean;
};

/** One round glass button of the column at the side of a reel, with its number under it. */
export default function RailButton({ icon, label, caption, onClick, className, pressed }: Props) {
   return (
      <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 0.25 }}>
         <IconButton
            className={className}
            aria-label={label}
            aria-pressed={pressed}
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
               '&:active': { transform: 'scale(0.92)' },
            }}
         >
            {icon}
         </IconButton>
         {caption !== undefined && caption !== '' && (
            <Typography
               className={`${className}-count`}
               variant="caption"
               sx={{ color: '#fff', fontWeight: 700, textShadow: '0 1px 4px rgba(0,0,0,0.6)' }}
            >
               {caption}
            </Typography>
         )}
      </Box>
   );
}
