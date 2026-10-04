import { Box, Typography } from '@mui/material';
import React from 'react';
import { Tone } from '../format';

export const toneColor = (tone: Tone) => (tone === 'critical' ? '#F87171' : tone === 'warning' ? '#FBBF24' : '#34D399');

type Props = {
   label: string;
   /** 0 to 100 */
   percent: number | null;
   tone: Tone;
   /** shown on the right, defaults to the percentage */
   valueText?: string;
   hint?: string;
};

/** A horizontal bar that is green, amber or red. */
export default function Meter({ label, percent, tone, valueText, hint }: Props) {
   const clamped = percent === null ? 0 : Math.min(100, Math.max(0, percent));

   return (
      <Box sx={{ mb: 1.5 }}>
         <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.5 }}>
            <Typography variant="body2" color="text.secondary">
               {label}
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 700 }}>
               {valueText ?? (percent === null ? '–' : `${percent.toFixed(0)}%`)}
            </Typography>
         </Box>
         <Box
            role="meter"
            aria-label={label}
            aria-valuemin={0}
            aria-valuemax={100}
            aria-valuenow={clamped}
            sx={{ height: 8, borderRadius: 4, backgroundColor: 'rgba(255,255,255,0.08)', overflow: 'hidden' }}
         >
            <Box
               sx={{
                  width: `${clamped}%`,
                  height: '100%',
                  borderRadius: 4,
                  backgroundColor: toneColor(tone),
                  transition: 'width 400ms ease, background-color 400ms ease',
               }}
            />
         </Box>
         {hint && (
            <Typography variant="caption" color="text.secondary">
               {hint}
            </Typography>
         )}
      </Box>
   );
}
