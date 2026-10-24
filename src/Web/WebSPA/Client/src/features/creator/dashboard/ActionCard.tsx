import { Box, Paper, Typography } from '@mui/material';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import ArrowForwardIcon from '@mui/icons-material/ArrowForward';
import React from 'react';
import { Link as RouterLink } from 'react-router-dom';
import { brand } from 'src/theme';

type Props = {
   id: string;
   title: string;
   text: string;
   icon: React.ReactNode;
   to: string;
   /** it needs something first (a bank account): the card says so and leads to where that is done */
   locked?: boolean;
   lockedText?: string;
   onLockedClick?: () => void;
   primary?: boolean;
};

/** A big button of the dashboard: an icon, what it is for, and an arrow. */
export default function ActionCard({ id, title, text, icon, to, locked, lockedText, onLockedClick, primary }: Props) {
   const common = {
      id,
      className: 'action-card',
      sx: {
         p: 2.5,
         display: 'flex',
         gap: 2,
         alignItems: 'center',
         textDecoration: 'none',
         color: 'inherit',
         cursor: 'pointer',
         textAlign: 'left',
         borderRadius: '20px',
         border: `1px solid ${primary && !locked ? 'transparent' : brand.border}`,
         background: primary && !locked ? brand.gradientSoft : undefined,
         opacity: locked ? 0.7 : 1,
         transition: 'transform 140ms ease, border-color 140ms ease, box-shadow 140ms ease',
         '&:hover': {
            transform: 'translateY(-2px)',
            borderColor: 'rgba(255,255,255,0.2)',
            boxShadow: `0 12px 30px -18px ${brand.accentShadow}`,
         },
      },
   } as const;

   const body = (
      <>
         <Box
            sx={{
               width: 48,
               height: 48,
               flexShrink: 0,
               borderRadius: '16px',
               display: 'grid',
               placeItems: 'center',
               color: '#fff',
               background: locked ? 'rgba(255,255,255,0.1)' : brand.gradient,
            }}
         >
            {locked ? <LockOutlinedIcon /> : icon}
         </Box>
         <Box sx={{ minWidth: 0, flex: 1 }}>
            <Typography sx={{ fontWeight: 800 }}>{title}</Typography>
            <Typography variant="body2" color="text.secondary">
               {locked ? lockedText ?? text : text}
            </Typography>
         </Box>
         <ArrowForwardIcon sx={{ opacity: 0.5, flexShrink: 0 }} fontSize="small" />
      </>
   );

   return locked ? (
      <Paper {...common} component="button" type="button" onClick={onLockedClick}>
         {body}
      </Paper>
   ) : (
      <Paper {...common} component={RouterLink} to={to}>
         {body}
      </Paper>
   );
}
