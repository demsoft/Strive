import { useAuth } from 'react-oidc-context';
import { Link as RouterLink } from 'react-router-dom';
import { Box, Button, Chip, Typography } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import GraphicEqIcon from '@mui/icons-material/GraphicEq';
import GroupsIcon from '@mui/icons-material/Groups';
import PaymentsOutlinedIcon from '@mui/icons-material/PaymentsOutlined';
import PianoIcon from '@mui/icons-material/Piano';
import AvTimerIcon from '@mui/icons-material/AvTimer';
import SchoolOutlinedIcon from '@mui/icons-material/SchoolOutlined';
import HighQualityIcon from '@mui/icons-material/HighQuality';
import MeetingRoomIcon from '@mui/icons-material/MeetingRoom';
import PanToolOutlinedIcon from '@mui/icons-material/PanToolOutlined';
import ScreenShareIcon from '@mui/icons-material/ScreenShare';
import React, { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { useDispatch, useSelector } from 'react-redux';
import { isGbedu } from 'src/brand-id';
import BrandLogo from 'src/components/BrandLogo';
import MyConferencesList from 'src/features/conference/components/MyConferencesList';
import { fetchConferenceLinks } from 'src/features/conference/reducer';
import ConferenceControls from 'src/features/create-conference/components/ConferenceControls';
import { RootState } from 'src/store';
import { brand } from 'src/theme';

const useStyles = makeStyles()((theme) => ({
   root: {
      minHeight: '100%',
      width: '100%',
      display: 'flex',
      flexDirection: 'column',
      boxSizing: 'border-box',
      padding: theme.spacing(3, 4),
      [theme.breakpoints.down('md')]: {
         padding: theme.spacing(2),
      },
   },
   header: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
   },
   main: {
      flex: 1,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      gap: theme.spacing(8),
      maxWidth: 1180,
      width: '100%',
      margin: '0 auto',
      padding: theme.spacing(4, 0),
      [theme.breakpoints.down('md')]: {
         flexDirection: 'column',
         gap: theme.spacing(5),
         justifyContent: 'center',
      },
   },
   hero: {
      flex: 1,
      minWidth: 0,
      [theme.breakpoints.down('md')]: {
         textAlign: 'center',
         display: 'flex',
         flexDirection: 'column',
         alignItems: 'center',
      },
   },
   gradientText: {
      backgroundImage: brand.gradient,
      WebkitBackgroundClip: 'text',
      backgroundClip: 'text',
      color: 'transparent',
      WebkitTextFillColor: 'transparent',
   },
   title: {
      fontSize: '3.6rem',
      lineHeight: 1.05,
      [theme.breakpoints.down('md')]: {
         fontSize: '2.4rem',
      },
   },
   subtitle: {
      marginTop: theme.spacing(2),
      marginBottom: theme.spacing(4),
      maxWidth: 520,
      color: theme.palette.text.secondary,
   },
   chips: {
      display: 'flex',
      flexWrap: 'wrap',
      gap: theme.spacing(1),
      marginTop: theme.spacing(4),
      [theme.breakpoints.down('md')]: {
         justifyContent: 'center',
      },
   },
   chip: {
      backgroundColor: 'rgba(255, 255, 255, 0.06)',
      border: `1px solid ${brand.border}`,
   },
   panel: {
      width: 380,
      maxWidth: '100%',
      flexShrink: 0,
      padding: theme.spacing(1, 1, 2),
      borderRadius: 24,
      backgroundColor: 'rgba(20, 24, 41, 0.72)',
      border: `1px solid ${brand.border}`,
      backdropFilter: 'blur(20px)',
      boxShadow: '0 24px 80px rgba(0, 0, 0, 0.4)',
   },
   panelTitle: {
      padding: theme.spacing(2, 2, 0.5),
   },
}));

export default function MainRoute() {
   const { classes } = useStyles();
   const dispatch = useDispatch();
   const { t } = useTranslation();

   useEffect(() => {
      dispatch(fetchConferenceLinks());
   }, [dispatch]);

   const links = useSelector((state: RootState) => state.conference.conferenceLinks);
   const auth = useAuth();
   const texts = isGbedu ? 'view_main_gbedu' : 'view_main';

   return (
      <div className={classes.root}>
         <header className={classes.header}>
            <BrandLogo size={34} />
            <Box sx={{ display: 'flex', gap: 1 }}>
               <Button color="inherit" component={RouterLink} to="/recordings" id="my-recordings-link">
                  {t('conference.recording.my_link')}
               </Button>
               <Button color="inherit" onClick={() => auth.signoutRedirect()}>
                  {t('common:sign_out')}
               </Button>
            </Box>
         </header>

         <main className={classes.main}>
            <section className={classes.hero}>
               <Typography variant="h1" className={classes.title}>
                  {t(`${texts}.headline_1`)} <span className={classes.gradientText}>{t(`${texts}.headline_2`)}</span>
               </Typography>
               <Typography variant="h6" component="p" className={classes.subtitle}>
                  {t(`${texts}.subtitle`)}
               </Typography>
               <ConferenceControls />
               <div className={classes.chips}>
                  {isGbedu ? (
                     <>
                        <Chip
                           className={classes.chip}
                           icon={<PianoIcon />}
                           label={t('view_main_gbedu.feature_sound')}
                        />
                        <Chip
                           className={classes.chip}
                           icon={<AvTimerIcon />}
                           label={t('view_main_gbedu.feature_click')}
                        />
                        <Chip
                           className={classes.chip}
                           icon={<SchoolOutlinedIcon />}
                           label={t('view_main_gbedu.feature_lesson')}
                        />
                        <Chip
                           className={classes.chip}
                           icon={<GraphicEqIcon />}
                           label={t('view_main_gbedu.feature_tracks')}
                        />
                        <Chip
                           className={classes.chip}
                           icon={<PaymentsOutlinedIcon />}
                           label={t('view_main_gbedu.feature_naira')}
                        />
                     </>
                  ) : (
                     <>
                        <Chip
                           className={classes.chip}
                           icon={<HighQualityIcon />}
                           label={t('view_main.feature_video')}
                        />
                        <Chip
                           className={classes.chip}
                           icon={<ScreenShareIcon />}
                           label={t('view_main.feature_screen')}
                        />
                        <Chip
                           className={classes.chip}
                           icon={<MeetingRoomIcon />}
                           label={t('view_main.feature_lobby')}
                        />
                        <Chip
                           className={classes.chip}
                           icon={<PanToolOutlinedIcon />}
                           label={t('view_main.feature_hands')}
                        />
                        <Chip className={classes.chip} icon={<GroupsIcon />} label={t('view_main.feature_rooms')} />
                     </>
                  )}
               </div>
            </section>

            {links && links.length > 0 && (
               <Box className={classes.panel} id="my-conferences">
                  <Typography variant="subtitle1" className={classes.panelTitle} sx={{ fontWeight: 700 }}>
                     {t('view_main.my_conferences')}
                  </Typography>
                  <MyConferencesList links={links} />
               </Box>
            )}
         </main>
      </div>
   );
}
