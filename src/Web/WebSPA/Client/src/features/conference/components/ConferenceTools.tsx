import { Box, IconButton, Tooltip } from '@mui/material';
import SettingsOutlinedIcon from '@mui/icons-material/SettingsOutlined';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { useDispatch } from 'react-redux';
import { useParams } from 'react-router';
import * as coreHub from 'src/core-hub';
import { openDialogToPatchAsync } from 'src/features/create-conference/reducer';
import { openSettings } from 'src/features/settings/reducer';
import useIsMobile from 'src/hooks/useIsMobile';
import usePermission from 'src/hooks/usePermission';
import { CONFERENCE_CAN_OPEN_AND_CLOSE, RECORDING_CAN_MANAGE } from 'src/permissions';
import { ConferenceRouteParams } from 'src/routes/types';
import RecordButton from 'src/features/recording/components/RecordButton';
import LatencyButton from 'src/features/latency/components/LatencyButton';
import MetronomeButton from 'src/features/metronome/components/MetronomeButton';
import LessonModeButton from 'src/features/lesson-mode/components/LessonModeButton';
import WatchTogetherButton from 'src/features/watch-together/components/WatchTogetherButton';
import { openRecordings } from 'src/features/recording/reducer';
import InviteLinkButton from 'src/components/InviteLinkButton';
import LobbyButton from 'src/features/lobby/components/LobbyButton';
import UserMenu from './appbar/UserMenu';

type Props = {
   /** the link of the room: a paid session must not hand it out (the ticket is the way in) */
   showInvite?: boolean;
};

/** What a person can do in the room: record, tools for teaching and music, settings and the menu of the account. */
export default function ConferenceTools({ showInvite = true }: Props) {
   const dispatch = useDispatch();
   const { t } = useTranslation();
   const isMobile = useIsMobile();
   const { id: conferenceId } = useParams<ConferenceRouteParams>();
   const canCloseConference = usePermission(CONFERENCE_CAN_OPEN_AND_CLOSE);
   const canManageRecordings = usePermission(RECORDING_CAN_MANAGE);

   const handlePatchConference = () => {
      if (!conferenceId) {
         console.error('Conference id must not be null');
         return;
      }
      dispatch(openDialogToPatchAsync(conferenceId));
   };

   return (
      <Box sx={{ display: 'flex', alignItems: 'center', gap: { xs: 0, sm: 0.5 }, flexShrink: 0 }}>
         <RecordButton />
         <WatchTogetherButton />
         <LessonModeButton />
         <MetronomeButton />
         <LatencyButton />
         <LobbyButton />
         {showInvite && <InviteLinkButton />}
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
            onShowRecordings={() => dispatch(openRecordings())}
            onChangeConference={handlePatchConference}
            onCloseConference={() => dispatch(coreHub.closeConference())}
            onOpenSettings={() => dispatch(openSettings())}
         />
      </Box>
   );
}
