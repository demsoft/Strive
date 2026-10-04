import { Box, Typography } from '@mui/material';
import React from 'react';

type Props = {
   title: string;
   values: (number | null)[];
   /** fixed top of the scale (for percentages 100), else the highest value */
   max?: number;
   color: string;
   /** value of the last point as text */
   currentText: string;
   /** horizontal reference lines (the warning and critical values) */
   lines?: { value: number; color: string }[];
   firstLabel?: string;
   lastLabel?: string;
};

const WIDTH = 320;
const HEIGHT = 80;

/** A small line chart in SVG: no library, the numbers are few. */
export default function Sparkline({ title, values, max, color, currentText, lines = [], firstLabel, lastLabel }: Props) {
   const numbers = values.filter((x): x is number => x !== null);
   const top = max ?? Math.max(1, ...numbers);

   const x = (i: number) => (values.length <= 1 ? WIDTH : (i / (values.length - 1)) * WIDTH);
   const y = (v: number) => HEIGHT - (Math.min(v, top) / top) * HEIGHT;

   let path = '';
   let started = false;
   values.forEach((v, i) => {
      if (v === null) {
         started = false;
         return;
      }
      path += `${started ? 'L' : 'M'}${x(i).toFixed(1)},${y(v).toFixed(1)} `;
      started = true;
   });

   const first = values.findIndex((v) => v !== null);
   const lastIndex = values.length - 1 - [...values].reverse().findIndex((v) => v !== null);
   const area =
      numbers.length > 1 && first >= 0
         ? `${path} L${x(lastIndex).toFixed(1)},${HEIGHT} L${x(first).toFixed(1)},${HEIGHT} Z`
         : '';

   return (
      <Box>
         <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', mb: 0.5 }}>
            <Typography variant="body2" color="text.secondary">
               {title}
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 700 }}>
               {currentText}
            </Typography>
         </Box>
         <svg
            viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
            width="100%"
            height={HEIGHT}
            preserveAspectRatio="none"
            role="img"
            aria-label={title}
            style={{ display: 'block' }}
         >
            {lines.map((line) => (
               <line
                  key={line.value}
                  x1={0}
                  x2={WIDTH}
                  y1={y(line.value)}
                  y2={y(line.value)}
                  stroke={line.color}
                  strokeOpacity={0.5}
                  strokeDasharray="4 4"
                  vectorEffect="non-scaling-stroke"
               />
            ))}
            {area && <path d={area} fill={color} fillOpacity={0.15} />}
            {path && (
               <path d={path} fill="none" stroke={color} strokeWidth={2} vectorEffect="non-scaling-stroke" strokeLinejoin="round" />
            )}
         </svg>
         <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
            <Typography variant="caption" color="text.secondary">
               {firstLabel}
            </Typography>
            <Typography variant="caption" color="text.secondary">
               {lastLabel}
            </Typography>
         </Box>
      </Box>
   );
}
