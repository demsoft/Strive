import { Box, Typography } from '@mui/material';
import React from 'react';
import { brandName, isArena, isGbedu } from 'src/brand-id';
import { brand } from 'src/theme';

type Props = {
   size?: number;
   /** show the word mark next to the logo mark */
   showName?: boolean;
};

/** The logo mark of the brand: a gradient tile with a stylized S (Strive), sound waves (Gbedu) or a ball (Arena). */
export default function BrandLogo({ size = 32, showName = true }: Props) {
   return (
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25 }}>
         <svg width={size} height={size} viewBox="0 0 32 32" role="img" aria-label={brandName}>
            <defs>
               <linearGradient id="brand-gradient" x1="0" y1="0" x2="1" y2="1">
                  <stop offset="0" stopColor={brand.violet} />
                  <stop offset="1" stopColor={brand.cyan} />
               </linearGradient>
            </defs>
            <rect width="32" height="32" rx="9" fill="url(#brand-gradient)" />
            {isArena ? (
               <>
                  <circle cx="16" cy="16" r="9" fill="none" stroke="#fff" strokeWidth="2.2" />
                  <path
                     d="M16 11.2l3.7 2.7-1.4 4.4h-4.6l-1.4-4.4zM16 11.2V7.2M19.7 13.9l3.8-1.2M18.3 18.3l2.4 3.2M13.7 18.3l-2.4 3.2M12.3 13.9l-3.8-1.2"
                     fill="none"
                     stroke="#fff"
                     strokeWidth="1.8"
                     strokeLinecap="round"
                     strokeLinejoin="round"
                  />
               </>
            ) : isGbedu ? (
               <>
                  <circle cx="16" cy="16" r="3" fill="#fff" />
                  <path
                     d="M10.6 10.6a7.6 7.6 0 0 0 0 10.8M21.4 10.6a7.6 7.6 0 0 1 0 10.8"
                     fill="none"
                     stroke="#fff"
                     strokeWidth="2.2"
                     strokeLinecap="round"
                  />
                  <path
                     d="M6.8 6.8a13 13 0 0 0 0 18.4M25.2 6.8a13 13 0 0 1 0 18.4"
                     fill="none"
                     stroke="#fff"
                     strokeOpacity=".6"
                     strokeWidth="2.2"
                     strokeLinecap="round"
                  />
               </>
            ) : (
               <path
                  d="M21.5 11.2c-1.2-1.4-3-2.2-5.2-2.2-3 0-5 1.5-5 3.7 0 2.2 1.7 3.1 4.7 3.8 2.3.5 3.4.9 3.4 2 0 1.1-1.2 1.8-3 1.8-1.7 0-3.1-.7-4.1-1.9"
                  fill="none"
                  stroke="#fff"
                  strokeWidth="2.6"
                  strokeLinecap="round"
               />
            )}
         </svg>
         {showName && (
            <Typography variant="h6" component="span" sx={{ fontWeight: 800, letterSpacing: '-0.02em' }}>
               {brandName}
            </Typography>
         )}
      </Box>
   );
}
