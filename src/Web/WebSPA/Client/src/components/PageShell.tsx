import { AppBar, Box, Container, Toolbar, Typography } from '@mui/material';
import React from 'react';
import { Link as RouterLink } from 'react-router-dom';
import BackButton from 'src/components/BackButton';
import BrandLogo from 'src/components/BrandLogo';
import { brand } from 'src/theme';

type Props = {
   /** the id of the page container */
   id: string;
   title: string;
   subtitle?: string;
   icon: React.ReactNode;
   /** where the back button goes when there is no history */
   backFallback: string;
   /** shown on the right of the top bar */
   toolbarEnd?: React.ReactNode;
   maxWidth?: 'sm' | 'md' | 'lg';
   children: React.ReactNode;
};

/** The frame of the pages of a creator: the top bar with the way back, a title with an icon, and the content. */
export default function PageShell({
   id,
   title,
   subtitle,
   icon,
   backFallback,
   toolbarEnd,
   maxWidth = 'md',
   children,
}: Props) {
   return (
      <Box sx={{ minHeight: '100%' }}>
         <AppBar position="static" color="transparent" elevation={0}>
            <Toolbar sx={{ gap: 2 }}>
               <BackButton fallback={backFallback} />
               <RouterLink to="/" style={{ textDecoration: 'none', color: 'inherit' }}>
                  <BrandLogo size={30} />
               </RouterLink>
               <Box sx={{ flex: 1 }} />
               {toolbarEnd}
            </Toolbar>
         </AppBar>

         <Container maxWidth={maxWidth} sx={{ py: { xs: 2, sm: 4 } }} id={id}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, mb: 3 }}>
               <Box
                  sx={{
                     width: 52,
                     height: 52,
                     flexShrink: 0,
                     borderRadius: '16px',
                     display: 'grid',
                     placeItems: 'center',
                     background: brand.gradient,
                     color: '#fff',
                     boxShadow: `0 8px 24px ${brand.accentShadow}`,
                  }}
               >
                  {icon}
               </Box>
               <Box sx={{ minWidth: 0 }}>
                  <Typography variant="h4" sx={{ fontWeight: 800, fontSize: { xs: '1.6rem', sm: '2rem' } }}>
                     {title}
                  </Typography>
                  {subtitle && (
                     <Typography color="text.secondary" variant="body2">
                        {subtitle}
                     </Typography>
                  )}
               </Box>
            </Box>
            {children}
         </Container>
      </Box>
   );
}
