import { Fab, Grid, Portal } from '@mui/material';
import React, { useContext } from 'react';
import { useTranslation } from 'react-i18next';
import { useDispatch, useSelector } from 'react-redux';
import * as coreHub from 'src/core-hub';
import MediaControlsContext from 'src/features/media/media-controls-context';
import usePermission from 'src/hooks/usePermission';
import { SCENES_CAN_PASS_TALKING_STICK, SCENES_CAN_TAKE_TALKING_STICK } from 'src/permissions';
import { selectTalkingStickIsMeSpeaker } from '../../selectors';
import AddToListFab from './AddToListFab';
import PassStickFab from './PassStickFab';

type FrameProps = {
   children: React.ReactNode;
};

export default function FrameWithPassFab({ children }: FrameProps) {
   const { t } = useTranslation();
   const dispatch = useDispatch();

   const canPass = usePermission(SCENES_CAN_PASS_TALKING_STICK);
   const canTake = usePermission(SCENES_CAN_TAKE_TALKING_STICK);

   const isPresenter = useSelector(selectTalkingStickIsMeSpeaker);

   const mediaContext = useContext(MediaControlsContext);

   const handleReturnStick = () => dispatch(coreHub.talkingStickReturn());
   const handleTake = () => dispatch(coreHub.talkingStickTake());

   return (
      <div>
         <Portal container={mediaContext.leftControlsContainer}>
            {canPass && (
               <Grid>
                  <PassStickFab />
               </Grid>
            )}
            {isPresenter && (
               <Grid>
                  <Fab variant="extended" color="secondary" onClick={handleReturnStick}>
                     {t('conference.scenes.talking_stick_modes.return_stick')}
                  </Fab>
               </Grid>
            )}
            {!isPresenter && !canPass && (
               <Grid>
                  <AddToListFab />
               </Grid>
            )}
            {canTake && !isPresenter && (
               <Grid>
                  <Fab variant="extended" color="primary" onClick={handleTake}>
                     {t('conference.scenes.talking_stick_modes.take_stick')}
                  </Fab>
               </Grid>
            )}
         </Portal>
         {children}
      </div>
   );
}
