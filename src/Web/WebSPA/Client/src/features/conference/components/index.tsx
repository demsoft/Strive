import React from 'react';
import { useSelector } from 'react-redux';
import useMyParticipantId from 'src/hooks/useMyParticipantId';
import useUpdateDeviceLabelsAutomatically from 'src/hooks/useUpdateDeviceLabelsAutomatically';
import { RootState } from 'src/store';
import { SynchronizedConferenceInfo } from 'src/store/signal/synchronization/synchronized-object-ids';
import useIsMobile from 'src/hooks/useIsMobile';
import ClassConference from './ClassConference';
import MobileConference from './MobileConference';
import ConferenceNotOpen from './conference-not-open/ConferenceNotOpen';
import ConferenceNotOpenModerator from './conference-not-open/ConferenceNotOpenModerator';
import RequestUserInteractionView from './RequestUserInteractionView';
import { isGbedu } from 'src/brand-id';
import GbeduLive from 'src/features/gbedu/live/GbeduLive';
import GbeduWaitingRoom from 'src/features/gbedu/live/GbeduWaitingRoom';

type Props = {
   conference: SynchronizedConferenceInfo;
};

export default function index({ conference }: Props) {
   const userInteractionMade = useSelector((state: RootState) => state.media.userInteractionMade);
   const playSoundOnConferenceOpen = useSelector((state: RootState) => state.settings.obj.conference.playSoundOnOpen);

   const isMobile = useIsMobile();
   const myId = useMyParticipantId();
   const isModerator = conference.moderators.includes(myId);

   useUpdateDeviceLabelsAutomatically();

   if (playSoundOnConferenceOpen && !userInteractionMade) {
      return <RequestUserInteractionView />;
   }

   if (!conference.isOpen) {
      if (isGbedu) return <GbeduWaitingRoom conferenceInfo={conference} isHost={isModerator} />;

      return isModerator ? (
         <ConferenceNotOpenModerator conferenceInfo={conference} />
      ) : (
         <ConferenceNotOpen conferenceInfo={conference} />
      );
   }

   if (!userInteractionMade) {
      return <RequestUserInteractionView />;
   }

   // Gbedu has its own room: a stage and a live chat instead of the tools of a meeting
   if (isGbedu && !isMobile) return <GbeduLive />;

   return isMobile ? <MobileConference /> : <ClassConference />;
}
