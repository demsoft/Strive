import { AppBar, Box, Chip, Divider, IconButton, Toolbar, Tooltip, Typography } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import PeopleAltOutlinedIcon from '@mui/icons-material/PeopleAltOutlined';
import SettingsOutlinedIcon from '@mui/icons-material/SettingsOutlined';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useDispatch, useSelector } from 'react-redux';
import { useParams } from 'react-router';
import * as coreHub from 'src/core-hub';
import { openDialogToPatchAsync } from 'src/features/create-conference/reducer';
import { openSettings } from 'src/features/settings/reducer';
import useIsMobile from 'src/hooks/useIsMobile';
import usePermission from 'src/hooks/usePermission';
import { CONFERENCE_CAN_OPEN_AND_CLOSE, RECORDING_CAN_MANAGE } from 'src/permissions';
import { ConferenceRouteParams } from 'src/routes/types';
import { RootState } from 'src/store';
import { selectParticipantList } from '../selectors';
import RecordButton from 'src/features/recording/components/RecordButton';
import WatchTogetherButton from 'src/features/watch-together/components/WatchTogetherButton';
import RecordingIndicator from 'src/features/recording/components/RecordingIndicator';
import RecordingsDialog from 'src/features/recording/components/RecordingsDialog';
import InviteLinkButton from 'src/components/InviteLinkButton';
import LobbyButton from 'src/features/lobby/components/LobbyButton';
import AppBarLogo from './appbar/AppBarLogo';
import BreakoutRoomChip from './appbar/BreakoutRoomChip';
import UserMenu from './appbar/UserMenu';
import WebRtcStatusChip from './appbar/WebRtcStatusChip';

const useStyles = makeStyles()((theme) => ({
   chip: {
      backgroundColor: 'rgba(255, 255, 255, 0.08)',
      fontWeight: 600,
   },
   breakoutRoomChip: {
      backgroundColor: theme.palette.primary.dark,
      minWidth: 0,
   },
}));

export default function ConferenceAppBar() {
   const { classes, cx } = useStyles();
   const dispatch = useDispatch();
   const { t } = useTranslation();

   const isMobile = useIsMobile();
   const conferenceName = useSelector((state: RootState) => state.conference.conferenceState?.name);
   const { id: conferenceId } = useParams<ConferenceRouteParams>();

   const canCloseConference = usePermission(CONFERENCE_CAN_OPEN_AND_CLOSE);
   const canManageRecordings = usePermission(RECORDING_CAN_MANAGE);
   const [recordingsOpen, setRecordingsOpen] = useState(false);
   const participants = useSelector(selectParticipantList);
   const breakoutRoomState = useSelector((state: RootState) => state.breakoutRooms.synchronized?.active);

   const handlePatchConference = () => {
      if (!conferenceId) {
         console.error('Conference id must not be null');
         return;
      }
      dispatch(openDialogToPatchAsync(conferenceId));
   };

   return (
      <AppBar position="static">
         <Toolbar variant="dense" sx={{ gap: { xs: 0.5, sm: 1.5 }, minHeight: 56, backgroundColor: 'transparent' }}>
            <AppBarLogo />
            {conferenceName && !isMobile && (
               <>
                  <Divider orientation="vertical" flexItem sx={{ my: 1.75 }} />
                  <Typography
                     variant="subtitle1"
                     noWrap
                     sx={{ fontWeight: 600, minWidth: 0, maxWidth: { sm: 160, md: 240, lg: 340 } }}
                     id="conference-name"
                  >
                     {conferenceName}
                  </Typography>
               </>
            )}

            {/* what is going on in the conference */}
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flex: 1, minWidth: 0, ml: { xs: 0.5, sm: 1 } }}>
               <RecordingIndicator />
               {breakoutRoomState && (
                  <BreakoutRoomChip className={cx(classes.chip, classes.breakoutRoomChip)} state={breakoutRoomState} />
               )}
               <WebRtcStatusChip />
               {participants && !isMobile && (
                  <Tooltip title={t('conference.appbar.participants', { count: participants.length })}>
                     <Chip
                        id="participant-count"
                        className={classes.chip}
                        icon={<PeopleAltOutlinedIcon />}
                        label={participants.length}
                        size="small"
                     />
                  </Tooltip>
               )}
            </Box>

            {/* what you can do */}
            <Box sx={{ display: 'flex', alignItems: 'center', gap: { xs: 0, sm: 0.5 }, flexShrink: 0 }}>
               <RecordButton />
               <WatchTogetherButton />
               <LobbyButton />
               <InviteLinkButton />
               {!isMobile && (
                  <Tooltip title={t('common:settings')}>
                     <IconButton aria-label={t('common:settings')} color="inherit" onClick={() => dispatch(openSettings())}>
                        <SettingsOutlinedIcon />
                     </IconButton>
                  </Tooltip>
               )}
               <UserMenu
                  showSettings={isMobile}
                  canManageRecordings={Boolean(canManageRecordings)}
                  canCloseConference={Boolean(canCloseConference)}
                  onShowPermissions={() => dispatch(coreHub.fetchPermissions(null))}
                  onShowRecordings={() => setRecordingsOpen(true)}
                  onChangeConference={handlePatchConference}
                  onCloseConference={() => dispatch(coreHub.closeConference())}
                  onOpenSettings={() => dispatch(openSettings())}
               />
            </Box>

            <RecordingsDialog open={recordingsOpen} onClose={() => setRecordingsOpen(false)} />
         </Toolbar>
      </AppBar>
   );
}
