import { Box, Button, Typography } from '@mui/material';
import ConfirmationNumberOutlinedIcon from '@mui/icons-material/ConfirmationNumberOutlined';
import LiveTvOutlinedIcon from '@mui/icons-material/LiveTvOutlined';
import PaymentsOutlinedIcon from '@mui/icons-material/PaymentsOutlined';
import React from 'react';
import { useAuth } from 'react-oidc-context';
import { useTranslation } from 'react-i18next';
import { makeStyles } from 'tss-react/mui';
import BrandLogo from 'src/components/BrandLogo';
import { brand } from 'src/theme';

const useStyles = makeStyles()((theme) => ({
   root: {
      minHeight: '100%',
      display: 'flex',
      flexDirection: 'column',
      padding: theme.spacing(3, 4),
      boxSizing: 'border-box',
      [theme.breakpoints.down('md')]: { padding: theme.spacing(2) },
   },
   header: { display: 'flex', alignItems: 'center', justifyContent: 'space-between' },
   main: {
      flex: 1,
      maxWidth: 960,
      width: '100%',
      margin: '0 auto',
      padding: theme.spacing(6, 0),
      textAlign: 'center',
   },
   gradientText: {
      backgroundImage: brand.gradient,
      WebkitBackgroundClip: 'text',
      backgroundClip: 'text',
      color: 'transparent',
      WebkitTextFillColor: 'transparent',
   },
   title: { fontSize: '3.6rem', lineHeight: 1.05, [theme.breakpoints.down('md')]: { fontSize: '2.4rem' } },
   subtitle: { margin: theme.spacing(2, 'auto', 4), maxWidth: 560, color: theme.palette.text.secondary },
   steps: {
      display: 'grid',
      gridTemplateColumns: 'repeat(3, 1fr)',
      gap: theme.spacing(2),
      marginTop: theme.spacing(8),
      textAlign: 'left',
      [theme.breakpoints.down('md')]: { gridTemplateColumns: '1fr' },
   },
   step: {
      padding: theme.spacing(2.5),
      borderRadius: 20,
      backgroundColor: 'rgba(20, 24, 41, 0.6)',
      border: `1px solid ${brand.border}`,
   },
}));

/**
 * What a visitor of Gbedu sees before signing in: what it is and where to sign up or sign in. People who come with the
 * link of a session land on the page of that session instead (it has its own buttons).
 */
export default function GbeduLanding() {
   const { classes } = useStyles();
   const { t } = useTranslation();
   const auth = useAuth();

   const signIn = () => auth.signinRedirect({ state: { url: '/' } });
   const signUp = () => auth.signinRedirect({ state: { url: '/' }, prompt: 'create' });

   return (
      <div className={classes.root} id="gbedu-landing">
         <header className={classes.header}>
            <BrandLogo size={34} />
            <Button color="inherit" onClick={signIn} id="landing-sign-in-top">
               {t('landing.sign_in')}
            </Button>
         </header>

         <main className={classes.main}>
            <Typography variant="h1" className={classes.title}>
               {t('view_main_gbedu.headline_1')}{' '}
               <span className={classes.gradientText}>{t('view_main_gbedu.headline_2')}</span>
            </Typography>
            <Typography variant="h6" component="p" className={classes.subtitle}>
               {t('view_main_gbedu.subtitle')}
            </Typography>

            <Box sx={{ display: 'flex', gap: 2, justifyContent: 'center', flexWrap: 'wrap' }}>
               <Button id="landing-sign-up" variant="contained" size="large" onClick={signUp}>
                  {t('landing.sign_up')}
               </Button>
               <Button id="landing-sign-in" variant="outlined" color="inherit" size="large" onClick={signIn}>
                  {t('landing.sign_in')}
               </Button>
            </Box>

            <div className={classes.steps}>
               <div className={classes.step}>
                  <ConfirmationNumberOutlinedIcon color="primary" />
                  <Typography variant="subtitle1" sx={{ fontWeight: 700, mt: 1 }}>
                     {t('landing.fans_title')}
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                     {t('landing.fans_text')}
                  </Typography>
               </div>
               <div className={classes.step}>
                  <LiveTvOutlinedIcon color="primary" />
                  <Typography variant="subtitle1" sx={{ fontWeight: 700, mt: 1 }}>
                     {t('landing.creators_title')}
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                     {t('landing.creators_text')}
                  </Typography>
               </div>
               <div className={classes.step}>
                  <PaymentsOutlinedIcon color="primary" />
                  <Typography variant="subtitle1" sx={{ fontWeight: 700, mt: 1 }}>
                     {t('landing.pay_title')}
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                     {t('landing.pay_text')}
                  </Typography>
               </div>
            </div>
         </main>
      </div>
   );
}
