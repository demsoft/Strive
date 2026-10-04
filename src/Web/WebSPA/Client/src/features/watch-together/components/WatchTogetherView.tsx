import React from 'react';
import { useSelector } from 'react-redux';
import { RenderSceneLayoutByType } from 'src/features/scenes/components/AutoSceneLayout';
import { selectParticipant } from 'src/features/conference/selectors';
import usePermission from 'src/hooks/usePermission';
import { WATCH_TOGETHER_CAN_CONTROL } from 'src/permissions';
import { RootState } from 'src/store';
import { WatchTogetherSession } from 'src/store/signal/synchronization/synchronized-object-ids';
import WatchTogetherPlayer from './WatchTogetherPlayer';

type Props = {
   session: WatchTogetherSession;
   width: number;
   height: number;
   className?: string;
};

/** Takes the place of the scene while a video is watched together: the video, with the active participants above it. */
export default function WatchTogetherView({ session, width, height, className }: Props) {
   const canControl = Boolean(usePermission(WATCH_TOGETHER_CAN_CONTROL));
   // the controls of the player cannot be switched on a running player: a new player when the permission changes
   const starter = useSelector((state: RootState) => selectParticipant(state, session.startedBy));

   return (
      <RenderSceneLayoutByType type="chips" width={width} height={height} className={className} participant={starter}>
         <div style={{ display: 'flex', justifyContent: 'center' }}>
            <WatchTogetherPlayer key={String(canControl)} session={session} canControl={canControl} />
         </div>
      </RenderSceneLayoutByType>
   );
}
