import { makeStyles } from 'tss-react/mui';
import React from 'react';
import ParticipantMicManager from 'src/features/media/components/ParticipantMicManager';
import SceneView from 'src/features/scenes/components/SceneView';
import useWebRtcStatus from 'src/store/webrtc/hooks/useWebRtcStatus';

const useStyles = makeStyles()({
   root: {
      width: '100vw',
      height: '100vh',
      overflow: 'hidden',
      position: 'relative',
   },
});

/**
 * What the recorder sees and records: only the scene of the conference (grid, active speaker, screen shares), without
 * any controls, sidebars or chat. The marker element tells the recorder service when it is ready to start recording.
 */
export default function RecordingView() {
   const { classes } = useStyles();
   const webRtcStatus = useWebRtcStatus();

   return (
      <ParticipantMicManager>
         <div className={classes.root} id="recording-view">
            <SceneView hideControls />
            {webRtcStatus === 'connected' && <div id="recording-ready" />}
         </div>
      </ParticipantMicManager>
   );
}
