import { Button, IconButton, Tooltip, Typography } from '@mui/material';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import SettingsIcon from '@mui/icons-material/Settings';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { useDispatch, useSelector } from 'react-redux';
import { useParams } from 'react-router-dom';
import * as coreHub from 'src/core-hub';
import { openDialogToPatchAsync } from 'src/features/create-conference/reducer';
import usePermission from 'src/hooks/usePermission';
import { CONFERENCE_CAN_OPEN_AND_CLOSE } from 'src/permissions';
import { ConferenceRouteParams } from 'src/routes/types';
import { SynchronizedConferenceInfo } from 'src/store/signal/synchronization/synchronized-object-ids';
import { selectParticipantList } from '../../selectors';
import ConferenceNotOpenLayout from './ConferenceNotOpenLayout';
import ConferenceNotOpenStatus from './ConferenceNotOpenStatus';

type Props = {
   conferenceInfo: SynchronizedConferenceInfo;
};

/** Shown to moderators: they decide when the conference starts. */
export default function ConferenceNotOpenModerator({ conferenceInfo }: Props) {
   const dispatch = useDispatch();
   const { t } = useTranslation();
   const { id: conferenceId } = useParams<ConferenceRouteParams>();

   const participants = useSelector(selectParticipantList);

   const canOpen = usePermission(CONFERENCE_CAN_OPEN_AND_CLOSE);
   const handleOpenConference = () => dispatch(coreHub.openConference());
   const handlePatchConference = () => {
      if (conferenceId) dispatch(openDialogToPatchAsync(conferenceId));
   };

   return (
      <ConferenceNotOpenLayout>
         <ConferenceNotOpenStatus
            name={conferenceInfo.name}
            scheduledDate={conferenceInfo.scheduledDate}
            title={t('conference_not_open.title_ready')}
            description={t('conference_not_open.you_are_moderator')}
         />
         <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', gap: 8, marginTop: 28 }}>
            <Button
               variant="contained"
               size="large"
               startIcon={<PlayArrowIcon />}
               onClick={handleOpenConference}
               disabled={!canOpen}
               id="moderator-open-conference-button"
            >
               {t('conference_not_open.open_conference')}
            </Button>
            <Tooltip title={t('conference_not_open.change_conference_settings')}>
               <IconButton
                  onClick={handlePatchConference}
                  aria-label={t('conference_not_open.change_conference_settings')}
                  id="moderator-change-conference-settings-button"
               >
                  <SettingsIcon />
               </IconButton>
            </Tooltip>
         </div>
         {participants.length > 1 && (
            <Typography color="textSecondary" variant="body2" sx={{ mt: 2 }}>
               {t('conference_not_open.n_participants_waiting', { count: participants.length - 1 })}
            </Typography>
         )}
      </ConferenceNotOpenLayout>
   );
}
