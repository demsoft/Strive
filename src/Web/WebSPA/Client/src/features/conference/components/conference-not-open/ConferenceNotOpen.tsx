import { Typography, useTheme } from '@mui/material';
import _ from 'lodash';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { useSelector } from 'react-redux';
import { SynchronizedConferenceInfo } from 'src/store/signal/synchronization/synchronized-object-ids';
import { selectParticipantList } from '../../selectors';
import ConferenceNotOpenLayout from './ConferenceNotOpenLayout';
import ConferenceNotOpenStatus from './ConferenceNotOpenStatus';

type Props = {
   conferenceInfo: SynchronizedConferenceInfo;
};

/** Shown to participants while the moderator has not opened the conference. */
export default function ConferenceNotOpen({ conferenceInfo }: Props) {
   const theme = useTheme();
   const { t } = useTranslation();
   const participants = useSelector(selectParticipantList);

   const isModeratorJoined = _.some(participants, (x) => conferenceInfo.moderators.includes(x.id));

   return (
      <ConferenceNotOpenLayout>
         <ConferenceNotOpenStatus
            name={conferenceInfo.name}
            scheduledDate={conferenceInfo.scheduledDate}
            title={t('conference_not_open.title_waiting')}
            description={
               isModeratorJoined
                  ? t('conference_not_open.waiting_for_moderator_to_open')
                  : t('conference_not_open.waiting_for_moderator_to_join')
            }
         />
         <Typography color="textSecondary" variant="body2" sx={{ mt: 2 }}>
            {participants.length > 1 && (
               <>
                  {t('conference_not_open.n_participants_waiting', { count: participants.length - 1 })}
                  {isModeratorJoined && (
                     <span style={{ color: theme.palette.secondary.main }}>
                        {' '}
                        {t('conference_not_open.moderator_joined')}
                     </span>
                  )}{' '}
               </>
            )}
            {t('conference_not_open.you_dont_need_to_refresh')}
         </Typography>
      </ConferenceNotOpenLayout>
   );
}
