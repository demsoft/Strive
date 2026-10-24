import { AppBar, Box, Button, Toolbar } from '@mui/material';
import ExploreOutlinedIcon from '@mui/icons-material/ExploreOutlined';
import SlideshowOutlinedIcon from '@mui/icons-material/SlideshowOutlined';
import React from 'react';
import { useAuth } from 'react-oidc-context';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, useLocation } from 'react-router-dom';
import BrandLogo from 'src/components/BrandLogo';
import { brand } from 'src/theme';

/** The top of the public pages: the logo, the two ways to look around (reels, creators), and the way in or home. */
export default function SiteHeader() {
   const { t } = useTranslation();
   const auth = useAuth();
   const location = useLocation();
   const here = (path: string) => location.pathname === path || location.pathname.startsWith(path + '/');

   const link = (to: string, label: string, icon: React.ReactNode, id: string) => (
      <Button
         id={id}
         component={RouterLink}
         to={to}
         color="inherit"
         aria-label={label}
         startIcon={icon}
         sx={{
            borderRadius: '999px',
            px: 1.75,
            minWidth: 0,
            opacity: here(to) ? 1 : 0.72,
            '& .MuiButton-startIcon': { mr: { xs: 0, sm: 1 }, ml: { xs: 0, sm: -0.5 } },
            backgroundColor: here(to) ? 'rgba(255,255,255,0.08)' : 'transparent',
            '&:hover': { opacity: 1 },
         }}
      >
         <Box component="span" sx={{ display: { xs: 'none', sm: 'inline' } }}>
            {label}
         </Box>
      </Button>
   );

   return (
      <AppBar
         position="sticky"
         color="transparent"
         elevation={0}
         sx={{
            backdropFilter: 'blur(14px)',
            backgroundColor: 'rgba(18,13,10,0.72)',
            borderBottom: `1px solid ${brand.border}`,
         }}
      >
         <Toolbar sx={{ gap: 1 }}>
            <RouterLink to="/" style={{ textDecoration: 'none', color: 'inherit' }}>
               <BrandLogo size={30} />
            </RouterLink>
            <Box sx={{ flex: 1 }} />
            {link('/reels', t('creators.nav_reels'), <SlideshowOutlinedIcon />, 'nav-reels')}
            {link('/creators', t('creators.nav_creators'), <ExploreOutlinedIcon />, 'nav-creators')}
            {auth.user ? (
               <Button id="nav-home" component={RouterLink} to="/" variant="outlined" color="inherit" sx={{ ml: 1 }}>
                  {t('creators.nav_home')}
               </Button>
            ) : (
               <Button
                  id="nav-sign-in"
                  variant="contained"
                  sx={{ ml: 1, background: brand.gradient, whiteSpace: 'nowrap', flexShrink: 0 }}
                  onClick={() => auth.signinRedirect({ state: { url: location.pathname } })}
               >
                  {t('creators.nav_sign_in')}
               </Button>
            )}
         </Toolbar>
      </AppBar>
   );
}
