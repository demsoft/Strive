import { Box, Paper, Skeleton, Typography } from '@mui/material';
import React from 'react';
import { Link as RouterLink } from 'react-router-dom';
import { brand } from 'src/theme';

type Props = {
   id: string;
   label: string;
   /** undefined while it is loading; null: it could not be loaded */
   value: string | null | undefined;
   hint?: string;
   icon: React.ReactNode;
   to?: string;
};

/** One number of the dashboard, with what it counts and a link to where it comes from. */
export default function StatCard({ id, label, value, hint, icon, to }: Props) {
   return (
      <Paper
         id={id}
         className="stat-card"
         {...(to ? { component: RouterLink, to } : {})}
         sx={{
            p: 2.25,
            display: 'flex',
            flexDirection: 'column',
            gap: 0.5,
            textDecoration: 'none',
            color: 'inherit',
            borderRadius: '20px',
            border: `1px solid ${brand.border}`,
            transition: 'transform 140ms ease, border-color 140ms ease',
            ...(to && { '&:hover': { transform: 'translateY(-2px)', borderColor: 'rgba(255,255,255,0.18)' } }),
         }}
      >
         <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <Typography variant="body2" color="text.secondary" sx={{ fontWeight: 600 }}>
               {label}
            </Typography>
            <Box
               sx={{
                  width: 34,
                  height: 34,
                  borderRadius: '12px',
                  display: 'grid',
                  placeItems: 'center',
                  background: brand.gradientSoft,
                  color: 'primary.light',
               }}
            >
               {icon}
            </Box>
         </Box>
         {value === undefined ? (
            <Skeleton variant="text" width={90} sx={{ fontSize: '2rem' }} />
         ) : (
            <Typography className="stat-value" sx={{ fontSize: '1.9rem', fontWeight: 800, lineHeight: 1.15 }}>
               {value ?? '—'}
            </Typography>
         )}
         {hint && (
            <Typography className="stat-hint" variant="caption" color="text.secondary">
               {hint}
            </Typography>
         )}
      </Paper>
   );
}
