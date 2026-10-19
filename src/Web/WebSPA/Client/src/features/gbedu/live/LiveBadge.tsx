import { Box } from '@mui/material';
import { keyframes } from 'tss-react';
import React from 'react';
import { useTranslation } from 'react-i18next';

const pulse = keyframes`
   0%, 100% { opacity: 1; transform: scale(1); }
   50% { opacity: 0.45; transform: scale(0.8); }
`;

/** "LIVE" with a pulsing red dot: the session is on. */
export default function LiveBadge() {
   const { t } = useTranslation();

   return (
      <Box
         id="live-badge"
         role="status"
         sx={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: 0.75,
            px: 1.25,
            py: 0.4,
            borderRadius: 99,
            fontSize: 12,
            fontWeight: 800,
            letterSpacing: 0.8,
            color: '#fff',
            backgroundColor: '#E5313F',
         }}
      >
         <Box
            component="span"
            sx={{
               width: 8,
               height: 8,
               borderRadius: '50%',
               backgroundColor: '#fff',
               animation: `${pulse} 1.4s ease-in-out infinite`,
            }}
         />
         {t('gbedu_live.live')}
      </Box>
   );
}
