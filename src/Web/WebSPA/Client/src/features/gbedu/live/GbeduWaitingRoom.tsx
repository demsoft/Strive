import { Box, Button, IconButton, Switch, Tooltip, Typography } from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import SettingsIcon from '@mui/icons-material/Settings';
import { DateTime } from 'luxon';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { useDispatch, useSelector } from 'react-redux';
import { useParams } from 'react-router-dom';
import { keyframes } from 'tss-react';
import { makeStyles } from 'tss-react/mui';
import BrandLogo from 'src/components/BrandLogo';
import * as coreHub from 'src/core-hub';
import ConferenceOpenSound from 'src/features/conference/components/conference-not-open/ConferenceOpenSound';
import { openDialogToPatchAsync } from 'src/features/create-conference/reducer';
import { isRecorderParticipant } from 'src/features/recording/recorder';
import { openSettings, setPlaySoundOnOpen } from 'src/features/settings/reducer';
import usePermission from 'src/hooks/usePermission';
import { CONFERENCE_CAN_OPEN_AND_CLOSE } from 'src/permissions';
import { ConferenceRouteParams } from 'src/routes/types';
import { RootState } from 'src/store';
import { SynchronizedConferenceInfo } from 'src/store/signal/synchronization/synchronized-object-ids';
import { brand } from 'src/theme';
import to from 'src/utils/to';
import { selectParticipantList } from 'src/features/conference/selectors';

const ring = keyframes`
   0% { transform: scale(0.6); opacity: 0.7; }
   100% { transform: scale(1.6); opacity: 0; }
`;

const useStyles = makeStyles()((theme) => ({
   root: {
      minHeight: '100%',
      display: 'flex',
      flexDirection: 'column',
      padding: theme.spacing(2, 3),
      boxSizing: 'border-box',
   },
   header: { display: 'flex', alignItems: 'center', justifyContent: 'space-between' },
   center: { flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: theme.spacing(3, 0) },
   card: {
      width: 520,
      maxWidth: '100%',
      boxSizing: 'border-box',
      padding: theme.spacing(5, 4),
      borderRadius: 28,
      textAlign: 'center',
      backgroundColor: 'rgba(29, 21, 17, 0.8)',
      border: `1px solid ${brand.border}`,
      boxShadow: '0 24px 80px rgba(0, 0, 0, 0.45)',
      [theme.breakpoints.down('sm')]: { padding: theme.spacing(4, 2) },
   },
   // the stage lights: rings that pulse while the audience waits
   lights: { position: 'relative', width: 96, height: 96, margin: '0 auto', marginBottom: theme.spacing(3) },
   ring: {
      position: 'absolute',
      inset: 0,
      borderRadius: '50%',
      border: `3px solid ${brand.violet}`,
      animation: `${ring} 2.4s ease-out infinite`,
   },
   core: {
      position: 'absolute',
      inset: 28,
      borderRadius: '50%',
      background: brand.gradient,
      boxShadow: `0 0 32px ${brand.accentShadow}`,
   },
}));

type Props = {
   conferenceInfo: SynchronizedConferenceInfo;
   /** the host decides when it starts, everybody else waits */
   isHost: boolean;
};

/**
 * Before the host opens the room. The audience waits on a stage with lights and is told that nothing has to be refreshed;
 * the host sees how many people wait and starts the session with one button. There is no link to share here: the room
 * belongs to the tickets.
 */
export default function GbeduWaitingRoom({ conferenceInfo, isHost }: Props) {
   const { classes } = useStyles();
   const { t } = useTranslation();
   const dispatch = useDispatch();
   const { id: conferenceId } = useParams<ConferenceRouteParams>();
   const participants = useSelector(selectParticipantList);
   const playSoundOnOpen = useSelector((state: RootState) => state.settings.obj.conference.playSoundOnOpen);
   const canOpen = usePermission(CONFERENCE_CAN_OPEN_AND_CLOSE);

   const waiting = participants.filter((x) => !isRecorderParticipant(x.id)).length - 1;
   const hostIsHere = participants.some((x) => conferenceInfo.moderators.includes(x.id));

   return (
      <div className={classes.root} id="gbedu-waiting">
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
            <div className={classes.card}>
               <div className={classes.lights} aria-hidden>
                  <div className={classes.ring} />
                  <div className={classes.ring} style={{ animationDelay: '1.2s' }} />
                  <div className={classes.core} />
               </div>

               {conferenceInfo.name && (
                  <Typography variant="overline" color="textSecondary" id="waiting-session-name">
                     {conferenceInfo.name}
                  </Typography>
               )}
               <Typography variant="h4" gutterBottom id="waiting-title">
                  {isHost ? t('gbedu_live.ready_title') : t('gbedu_live.waiting_title')}
               </Typography>
               {conferenceInfo.scheduledDate && (
                  <Typography color="textSecondary" gutterBottom>
                     {t('gbedu_live.scheduled_for')}{' '}
                     <b>{DateTime.fromISO(conferenceInfo.scheduledDate).toLocaleString(DateTime.DATETIME_FULL)}</b>
                  </Typography>
               )}
               <Typography color="textSecondary">
                  {isHost
                     ? t('gbedu_live.ready_text')
                     : hostIsHere
                     ? t('gbedu_live.host_is_here')
                     : t('gbedu_live.host_not_here')}
               </Typography>

               {isHost ? (
                  <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', gap: 1, mt: 3.5 }}>
                     <Button
                        variant="contained"
                        size="large"
                        startIcon={<PlayArrowIcon />}
                        onClick={() => dispatch(coreHub.openConference())}
                        disabled={!canOpen}
                        id="moderator-open-conference-button"
                     >
                        {t('gbedu_live.go_live')}
                     </Button>
                     <Tooltip title={t('conference_not_open.change_conference_settings')}>
                        <IconButton
                           onClick={() => conferenceId && dispatch(openDialogToPatchAsync(conferenceId))}
                           aria-label={t('conference_not_open.change_conference_settings')}
                           id="moderator-change-conference-settings-button"
                        >
                           <SettingsIcon />
                        </IconButton>
                     </Tooltip>
                  </Box>
               ) : (
                  <Typography color="textSecondary" variant="body2" sx={{ mt: 2 }}>
                     {t('gbedu_live.no_refresh')}
                  </Typography>
               )}

               {waiting > 0 && (
                  <Typography color="textSecondary" variant="body2" sx={{ mt: 2 }} id="waiting-count">
                     {t('gbedu_live.n_waiting', { count: waiting })}
                  </Typography>
               )}

               <label style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', marginTop: 24 }}>
                  <Switch
                     checked={Boolean(playSoundOnOpen)}
                     onChange={(_, value) => dispatch(setPlaySoundOnOpen(value))}
                  />
                  <Typography variant="body2" color="textSecondary">
                     {t('gbedu_live.sound_on_start')}
                  </Typography>
               </label>
            </div>
         </div>
         <ConferenceOpenSound />
      </div>
   );
}
