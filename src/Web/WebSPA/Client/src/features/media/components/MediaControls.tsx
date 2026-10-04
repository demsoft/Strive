import { Dialog, DialogContent, DialogTitle, Fab, Grid, Tooltip } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import BugReportIcon from '@mui/icons-material/BugReport';
import { motion } from 'framer-motion';
import React, { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useDispatch, useSelector } from 'react-redux';
import AnimatedCamIcon from 'src/assets/animated-icons/AnimatedCamIcon';
import AnimatedMicIcon from 'src/assets/animated-icons/AnimatedMicIcon';
import AnimatedScreenIcon from 'src/assets/animated-icons/AnimatedScreenIcon';
import Debug from 'src/features/conference/components/troubleshoot/Troubleshooting';
import { selectIsDeviceAvailableFactory } from 'src/features/settings/selectors';
import useIsMobile from 'src/hooks/useIsMobile';
import usePermission from 'src/hooks/usePermission';
import useSelectorFactory from 'src/hooks/useSelectorFactory';
import HandRaiseFab from 'src/features/hand-raise/components/HandRaiseFab';
import ReactionPicker from 'src/features/reactions/components/ReactionPicker';
import {
   HAND_RAISE_CAN_RAISE,
   MEDIA_CAN_SHARE_AUDIO,
   MEDIA_CAN_SHARE_SCREEN,
   MEDIA_CAN_SHARE_WEBCAM,
   REACTIONS_CAN_SEND,
} from 'src/permissions';
import { RootState } from 'src/store';
import { showMessage } from 'src/store/notifier/actions';
import useWebRtcStatus from 'src/store/webrtc/hooks/useWebRtcStatus';
import { formatErrorMessage } from 'src/utils/error-utils';
import useMicrophone from 'src/store/webrtc/hooks/useMicrophone';
import useScreen from 'src/store/webrtc/hooks/useScreen';
import useWebcam from 'src/store/webrtc/hooks/useWebcam';
import useDeviceManagement from '../useDeviceManagement';
import LeaveConferenceButton from 'src/features/conference/components/LeaveConferenceButton';
import MediaFab from './MediaFab';

const useStyles = makeStyles()((theme) => ({
   root: {
      display: 'flex',
      flexDirection: 'row',
      backgroundImage: 'linear-gradient(to bottom, rgba(11, 13, 23, 0), rgba(11, 13, 23, 0.75), rgba(11, 13, 23, 1))',
      padding: theme.spacing(3, 2, 2),
      alignItems: 'center',
      [theme.breakpoints.down('md')]: {
         padding: theme.spacing(2, 1, 1),
      },
   },
   leftActions: {
      flex: 1,
      display: 'flex',
      flexDirection: 'row',
      [theme.breakpoints.down('md')]: { flex: '0 0 auto' },
   },
   rightActions: {
      flex: 1,
      display: 'flex',
      flexDirection: 'row-reverse',
      [theme.breakpoints.down('md')]: { flex: '0 0 auto' },
   },
   fab: {
      margin: theme.spacing(0, 1),
      [theme.breakpoints.down('md')]: { margin: theme.spacing(0, 0.5) },
   },
   dialog: {
      backgroundColor: theme.palette.background.default,
   },
   controlsContainer: {
      display: 'flex',
      flexDirection: 'row',
      justifyContent: 'center',
      flex: 1,
   },
}));

type Props = {
   className?: string;
   show: boolean;
   leftActionsRef: React.Ref<HTMLDivElement>;
};

const variants = {
   visible: {
      opacity: 1,
      transition: {
         staggerChildren: 0.1,
      },
   },
   hidden: {
      opacity: 0,
   },
};

const item = {
   visible: { opacity: 1, scale: 1 },
   hidden: { opacity: 0, scale: 0 },
};

export default function MediaControls({ className, show, leftActionsRef }: Props) {
   const { classes, cx } = useStyles();
   const { t } = useTranslation();
   const isMobile = useIsMobile();
   const fabSize = isMobile ? 'medium' : 'large';

   const gain = useSelector((state: RootState) => state.settings.obj.mic.audioGain);

   const localMic = useMicrophone(gain);
   const audioDevice = useSelector((state: RootState) => state.settings.obj.mic.device);
   const micController = useDeviceManagement('mic', localMic, audioDevice);
   const micAvailable = useSelectorFactory(selectIsDeviceAvailableFactory, (state: RootState, selector) =>
      selector(state, 'mic'),
   );

   const webcamDevice = useSelector((state: RootState) => state.settings.obj.webcam.device);
   const localWebcam = useWebcam();
   const webcamController = useDeviceManagement('webcam', localWebcam, webcamDevice);
   const webcamAvailable = useSelectorFactory(selectIsDeviceAvailableFactory, (state: RootState, selector) =>
      selector(state, 'webcam'),
   );

   const screenDevice = useSelector((state: RootState) => state.settings.obj.screen.device);
   const localScreen = useScreen();
   const screenController = useDeviceManagement('screen', localScreen, screenDevice);
   const screenAvailable = useSelectorFactory(selectIsDeviceAvailableFactory, (state: RootState, selector) =>
      selector(state, 'screen'),
   );

   const canShareScreen = usePermission(MEDIA_CAN_SHARE_SCREEN);
   const canShareAudio = usePermission(MEDIA_CAN_SHARE_AUDIO);
   const canShareWebcam = usePermission(MEDIA_CAN_SHARE_WEBCAM);
   const canRaiseHand = usePermission(HAND_RAISE_CAN_RAISE);
   const canSendReaction = usePermission(REACTIONS_CAN_SEND);

   // turn on the devices the participant chose on the pre-join screen, once the media connection is ready
   const dispatch = useDispatch();
   const webRtcStatus = useWebRtcStatus();
   const joinWithMic = useSelector((state: RootState) => Boolean(state.settings.obj.conference.joinWithMic));
   const joinWithWebcam = useSelector((state: RootState) => Boolean(state.settings.obj.conference.joinWithWebcam));
   const autoEnabled = useRef(false);

   useEffect(() => {
      if (autoEnabled.current || webRtcStatus !== 'connected') return;
      autoEnabled.current = true;

      const enable = async (wanted: boolean, allowed: boolean, available: boolean, enableDevice: () => unknown) => {
         if (!wanted || !allowed || !available) return;
         try {
            await enableDevice();
         } catch (error) {
            dispatch(showMessage({ type: 'error', message: formatErrorMessage(error) }));
         }
      };

      enable(joinWithWebcam, Boolean(canShareWebcam), webcamAvailable, webcamController.enable);
      enable(joinWithMic, Boolean(canShareAudio), micAvailable, micController.enable);
   }, [webRtcStatus]);

   const [debugDialogOpen, setDebugDialogOpen] = useState(false);

   const handleCloseDebugDialog = () => setDebugDialogOpen(false);
   const handleOpenDebugDialog = () => setDebugDialogOpen(true);

   return (
      <motion.div
         className={cx(classes.root, className)}
         initial="hidden"
         animate={show ? 'visible' : 'hidden'}
         variants={variants}
      >
         <Grid container spacing={1} className={classes.leftActions} ref={leftActionsRef} />
         <div className={classes.controlsContainer}>
            {canShareScreen && screenAvailable && (
               <MediaFab
                  translationKey="screen"
                  className={classes.fab}
                  size={fabSize}
                  Icon={AnimatedScreenIcon}
                  control={screenController}
                  component={motion.button}
                  variants={item}
               />
            )}
            {canShareWebcam && (
               <MediaFab
                  disabled={!webcamAvailable}
                  translationKey="webcam"
                  warnWhenOff
                  className={classes.fab}
                  size={fabSize}
                  Icon={AnimatedCamIcon}
                  control={webcamController}
                  component={motion.button}
                  variants={item}
               />
            )}
            {canShareAudio && (
               <MediaFab
                  disabled={!micAvailable}
                  translationKey="mic"
                  warnWhenOff
                  className={classes.fab}
                  size={fabSize}
                  Icon={AnimatedMicIcon}
                  control={micController}
                  pauseOnToggle
                  component={motion.button}
                  variants={item}
               />
            )}
            {canRaiseHand && <HandRaiseFab className={classes.fab} size={fabSize} variants={item} />}
            {canSendReaction && <ReactionPicker className={classes.fab} size={fabSize} variants={item} />}
            <LeaveConferenceButton className={classes.fab} size={fabSize} />
         </div>
         <div className={classes.rightActions}>
            <Tooltip title={t('conference.troubleshooting.title')} arrow>
               <Fab
                  id="media-controls-troubleshooting"
                  color="default"
                  className={classes.fab}
                  size={fabSize}
                  onClick={handleOpenDebugDialog}
                  component={motion.button}
                  variants={item}
                  aria-label={t('conference.troubleshooting.title')}
               >
                  <BugReportIcon />
               </Fab>
            </Tooltip>
         </div>
         <Dialog
            id="troubleshooting-dialog"
            open={debugDialogOpen}
            onClose={handleCloseDebugDialog}
            slotProps={{
               paper: { className: classes.dialog },
            }}
         >
            <DialogTitle>{t('conference.troubleshooting.title')}</DialogTitle>
            <DialogContent>
               <Debug />
            </DialogContent>
         </Dialog>
      </motion.div>
   );
}
