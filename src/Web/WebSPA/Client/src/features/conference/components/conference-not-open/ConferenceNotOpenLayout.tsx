import { Box, Button, IconButton, Switch, Tooltip, Typography } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { useDispatch, useSelector } from 'react-redux';
import BrandLogo from 'src/components/BrandLogo';
import useInviteLink from 'src/hooks/useInviteLink';
import { openSettings, setPlaySoundOnOpen } from 'src/features/settings/reducer';
import { RootState } from 'src/store';
import { brand } from 'src/theme';
import to from 'src/utils/to';
import ConferenceOpenSound from './ConferenceOpenSound';

const useStyles = makeStyles()((theme) => ({
   root: {
      minHeight: '100%',
      display: 'flex',
      flexDirection: 'column',
      boxSizing: 'border-box',
      padding: theme.spacing(2, 3),
   },
   header: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
   },
   center: {
      flex: 1,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      padding: theme.spacing(3, 0),
   },
   card: {
      width: 520,
      maxWidth: '100%',
      boxSizing: 'border-box',
      padding: theme.spacing(5, 4),
      borderRadius: 28,
      textAlign: 'center',
      backgroundColor: 'rgba(20, 24, 41, 0.72)',
      border: `1px solid ${brand.border}`,
      backdropFilter: 'blur(20px)',
      boxShadow: '0 24px 80px rgba(0, 0, 0, 0.4)',
      [theme.breakpoints.down('sm')]: {
         padding: theme.spacing(4, 2),
      },
   },
   linkRow: {
      display: 'flex',
      alignItems: 'center',
      gap: theme.spacing(1),
      marginTop: theme.spacing(4),
      padding: theme.spacing(0.75, 0.75, 0.75, 2),
      borderRadius: 14,
      backgroundColor: 'rgba(255, 255, 255, 0.05)',
      border: `1px solid ${brand.border}`,
   },
   link: {
      flex: 1,
      minWidth: 0,
      textAlign: 'left',
      overflow: 'hidden',
      textOverflow: 'ellipsis',
      whiteSpace: 'nowrap',
   },
   soundRow: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      marginTop: theme.spacing(3),
   },
}));

type Props = {
   children: React.ReactNode;
};

/** The frame of the screens that are shown while the conference is not open yet. */
export default function ConferenceNotOpenLayout({ children }: Props) {
   const { classes } = useStyles();
   const dispatch = useDispatch();
   const { t } = useTranslation();
   const { link, copy } = useInviteLink();

   const playSoundOnOpen = useSelector((state: RootState) => state.settings.obj.conference.playSoundOnOpen);

   return (
      <div className={classes.root}>
         <div className={classes.header}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
               <Tooltip title={t('common:back_to_start')}>
                  <IconButton {...to('/')} aria-label={t('common:back_to_start')}>
                     <ArrowBackIcon />
                  </IconButton>
               </Tooltip>
               <BrandLogo size={28} />
            </Box>
            <Button color="inherit" onClick={() => dispatch(openSettings())} id="change-settings-button">
               {t('common:settings')}
            </Button>
         </div>

         <div className={classes.center}>
            <div className={classes.card} id="conference-not-open">
               {children}

               <div className={classes.linkRow}>
                  <Typography variant="body2" color="textSecondary" className={classes.link}>
                     {link}
                  </Typography>
                  <Button size="small" variant="outlined" startIcon={<ContentCopyIcon />} onClick={copy}>
                     {t('conference.invite.copy_short')}
                  </Button>
               </div>

               <label className={classes.soundRow}>
                  <Switch
                     checked={Boolean(playSoundOnOpen)}
                     onChange={(_, value) => dispatch(setPlaySoundOnOpen(value))}
                  />
                  <Typography variant="body2" color="textSecondary">
                     {t('conference_not_open.play_sound_on_conference_open')}
                  </Typography>
               </label>
            </div>
         </div>
         <ConferenceOpenSound />
      </div>
   );
}
