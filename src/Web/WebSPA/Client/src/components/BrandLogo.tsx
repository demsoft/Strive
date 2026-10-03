import { Box, Typography } from '@mui/material';
import React from 'react';
import { brand } from 'src/theme';

type Props = {
   size?: number;
   /** show the word mark next to the logo mark */
   showName?: boolean;
};

/** The Strive logo mark: a gradient tile with a stylized S. */
export default function BrandLogo({ size = 32, showName = true }: Props) {
   return (
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25 }}>
         <svg width={size} height={size} viewBox="0 0 32 32" role="img" aria-label="Strive">
            <defs>
               <linearGradient id="brand-gradient" x1="0" y1="0" x2="1" y2="1">
                  <stop offset="0" stopColor={brand.violet} />
                  <stop offset="1" stopColor={brand.cyan} />
               </linearGradient>
            </defs>
            <rect width="32" height="32" rx="9" fill="url(#brand-gradient)" />
            <path
               d="M21.5 11.2c-1.2-1.4-3-2.2-5.2-2.2-3 0-5 1.5-5 3.7 0 2.2 1.7 3.1 4.7 3.8 2.3.5 3.4.9 3.4 2 0 1.1-1.2 1.8-3 1.8-1.7 0-3.1-.7-4.1-1.9"
               fill="none"
               stroke="#fff"
               strokeWidth="2.6"
               strokeLinecap="round"
            />
         </svg>
         {showName && (
            <Typography variant="h6" component="span" sx={{ fontWeight: 800, letterSpacing: '-0.02em' }}>
               Strive
            </Typography>
         )}
      </Box>
   );
}
