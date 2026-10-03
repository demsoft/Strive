import { Typography } from '@mui/material';
import React, { useEffect, useMemo, useRef } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { RouteComponentProps } from 'react-router-dom';
import FullscreenError from 'src/components/FullscreenError';
import * as coreHub from 'src/core-hub';
import { setParticipantId } from 'src/features/auth/reducer';
import ConferenceConnecting from 'src/features/conference/components/ConferenceConnecting';
import { userInteractionMade } from 'src/features/media/reducer';
import RecordingView from 'src/features/recording/components/RecordingView';
import { parseRecorderToken } from 'src/features/recording/recorder';
import { RootState } from 'src/store';
import { close } from 'src/store/signal/actions';
import { WebRtcContext } from 'src/store/webrtc/WebRtcContext';
import { WebRtcManager } from 'src/store/webrtc/WebRtcManager';
import { formatErrorMessage } from 'src/utils/error-utils';
import { ConferenceRouteParams } from './types';

// no chat, no equipment and no reactions to send: the recorder only receives the scene
const defaultEvents: string[] = [
   coreHub.events.onSynchronizeObjectState,
   coreHub.events.onSynchronizedObjectUpdated,
   coreHub.events.onRequestDisconnect,
   coreHub.events.onReaction,
];

type Props = RouteComponentProps<ConferenceRouteParams>;

/**
 * Opened by the recorder service (a headless browser) to record the conference. It joins with a recorder token instead
 * of signing in, receives media only and renders just the scene.
 */
export default function RecordingRoute({
   location,
   match: {
      params: { id },
   },
}: Props) {
   const dispatch = useDispatch();
   const credentials = useMemo(() => parseRecorderToken(location.hash), [location.hash]);
   const webRtc = useRef(new WebRtcManager({ sendMedia: false, receiveMedia: true })).current;

   const error = useSelector((state: RootState) => state.conference.connectionError);
   const conferenceState = useSelector((state: RootState) => state.conference.conferenceState);
   const { isConnected, isReconnecting } = useSelector((state: RootState) => state.signalr);

   useEffect(() => {
      if (!credentials) return;

      // nobody clicks in the headless browser, audio must play without a gesture
      dispatch(userInteractionMade());
      dispatch(setParticipantId(credentials.participantId));
      dispatch(coreHub.joinConference(id, defaultEvents, credentials.token));
      webRtc.beginConnecting();

      return () => {
         dispatch(close());
      };
   }, [id, credentials, dispatch, webRtc]);

   if (!credentials) return <Typography id="recording-no-token">No recorder token provided.</Typography>;
   if (error) return <FullscreenError message={formatErrorMessage(error)} />;
   if (!conferenceState || !isConnected) return <ConferenceConnecting isReconnecting={isReconnecting} />;

   // the recorder is only started after the conference opened, nothing to record before
   if (!conferenceState.isOpen) return <div id="recording-conference-closed" />;

   return (
      <WebRtcContext.Provider value={webRtc}>
         <RecordingView />
      </WebRtcContext.Provider>
   );
}
