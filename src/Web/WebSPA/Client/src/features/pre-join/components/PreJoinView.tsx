import { Alert, Box, Button, LinearProgress, Typography } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import MicIcon from '@mui/icons-material/Mic';
import MicOffIcon from '@mui/icons-material/MicOff';
import VideocamIcon from '@mui/icons-material/Videocam';
import VideocamOffIcon from '@mui/icons-material/VideocamOff';
import React, { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { useDispatch, useSelector } from 'react-redux';
import BrandLogo from 'src/components/BrandLogo';
import useUser from 'src/features/auth/useUser';
import { userInteractionMade } from 'src/features/media/reducer';
import DeviceSelector from 'src/features/settings/components/DeviceSelector';
import { setCurrentDevice, setJoinWithMic, setJoinWithWebcam } from 'src/features/settings/reducer';
import { selectAvailableInputDevicesFactory } from 'src/features/settings/selectors';
import { fetchDevices } from 'src/features/settings/thunks';
import useSelectorFactory from 'src/hooks/useSelectorFactory';
import { RootState } from 'src/store';
import { brand } from 'src/theme';
import { useAudioLevel, useMediaPreview } from '../media-preview';

const useStyles = makeStyles()((theme) => ({
   root: {
      minHeight: '100%',
      display: 'flex',
      flexDirection: 'column',
      boxSizing: 'border-box',
      padding: theme.spacing(2, 3),
      [theme.breakpoints.down('md')]: { padding: theme.spacing(2) },
   },
   body: {
      flex: 1,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      gap: theme.spacing(6),
      maxWidth: 1100,
      width: '100%',
      margin: '0 auto',
      padding: theme.spacing(3, 0),
      [theme.breakpoints.down('md')]: { flexDirection: 'column', gap: theme.spacing(3) },
   },
   previewColumn: { flex: '1 1 560px', minWidth: 0, width: '100%', maxWidth: 640 },
   preview: {
      position: 'relative',
      width: '100%',
      paddingBottom: '56.25%',
      borderRadius: 24,
      overflow: 'hidden',
      backgroundColor: theme.palette.background.paper,
      border: `1px solid ${brand.border}`,
      boxShadow: '0 24px 80px rgba(0, 0, 0, 0.45)',
   },
   previewInner: {
      position: 'absolute',
      inset: 0,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
   },
   video: { width: '100%', height: '100%', objectFit: 'cover', transform: 'scaleX(-1)' },
   avatar: {
      width: 96,
      height: 96,
      borderRadius: '50%',
      backgroundImage: brand.gradient,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: '2.5rem',
      fontWeight: 700,
   },
   toggles: {
      position: 'absolute',
      bottom: theme.spacing(2),
      left: 0,
      right: 0,
      display: 'flex',
      justifyContent: 'center',
      gap: theme.spacing(2),
   },
   toggle: {
      width: 52,
      height: 52,
      borderRadius: '50%',
      border: 0,
      cursor: 'pointer',
      color: '#fff',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      backgroundColor: 'rgba(255, 255, 255, 0.16)',
      backdropFilter: 'blur(14px)',
      transition: 'background-color 150ms ease',
      '&:hover': { backgroundColor: 'rgba(255, 255, 255, 0.26)' },
   },
   toggleOff: {
      backgroundColor: theme.palette.error.main,
      '&:hover': { backgroundColor: theme.palette.error.dark },
   },
   side: { flex: '1 1 340px', minWidth: 0, width: '100%', maxWidth: 420 },
   selects: { display: 'flex', flexDirection: 'column', gap: theme.spacing(2), margin: theme.spacing(3, 0) },
   meter: { height: 6, borderRadius: 3, marginTop: theme.spacing(1) },
}));

type Props = {
   onJoin: () => void;
};

/**
 * The step before the conference connection is created: check camera and microphone, choose devices and decide
 * whether to join with them turned on. Nothing is sent to the server until the participant presses "Join now", so
 * nobody sees them waiting here.
 */
export default function PreJoinView({ onJoin }: Props) {
   const { classes, cx } = useStyles();
   const { t } = useTranslation();
   const dispatch = useDispatch();
   const user = useUser();

   const joinWithMic = useSelector((state: RootState) => Boolean(state.settings.obj.conference.joinWithMic));
   const joinWithWebcam = useSelector((state: RootState) => Boolean(state.settings.obj.conference.joinWithWebcam));
   const micDevice = useSelector((state: RootState) => state.settings.obj.mic.device);
   const webcamDevice = useSelector((state: RootState) => state.settings.obj.webcam.device);

   const micDevices = useSelectorFactory(selectAvailableInputDevicesFactory, (state: RootState, selector) =>
      selector(state, 'mic'),
   );
   const webcamDevices = useSelectorFactory(selectAvailableInputDevicesFactory, (state: RootState, selector) =>
      selector(state, 'webcam'),
   );

   const camera = useMediaPreview('video', webcamDevice, joinWithWebcam);
   const microphone = useMediaPreview('audio', micDevice, joinWithMic);
   const level = useAudioLevel(microphone.stream);

   const videoRef = useRef<HTMLVideoElement>(null);
   useEffect(() => {
      if (videoRef.current) videoRef.current.srcObject = camera.stream;
   }, [camera.stream]);

   // device names are only available once the browser granted access, so refresh the list after a preview started
   const hasStream = Boolean(camera.stream || microphone.stream);
   useEffect(() => {
      if (hasStream) dispatch(fetchDevices());
   }, [hasStream]);

   const name = user.profile.name ?? '';
   const initials = name.slice(0, 1).toUpperCase();
   const denied = camera.error === 'denied' || microphone.error === 'denied';
   const unavailable = camera.error === 'unavailable' || microphone.error === 'unavailable';

   const handleJoin = () => {
      dispatch(userInteractionMade());
      onJoin();
   };

   return (
      <div className={classes.root} id="pre-join">
         <BrandLogo size={30} />

         <div className={classes.body}>
            <div className={classes.previewColumn}>
               <div className={classes.preview}>
                  <div className={classes.previewInner}>
                     {camera.stream ? (
                        <video ref={videoRef} className={classes.video} autoPlay playsInline muted id="pre-join-video" />
                     ) : (
                        <div className={classes.avatar}>{initials}</div>
                     )}
                  </div>
                  <div className={classes.toggles}>
                     <button
                        type="button"
                        id="pre-join-toggle-mic"
                        className={cx(classes.toggle, !joinWithMic && classes.toggleOff)}
                        aria-pressed={joinWithMic}
                        aria-label={joinWithMic ? t('pre_join.mic_on') : t('pre_join.mic_off')}
                        title={joinWithMic ? t('pre_join.mic_on') : t('pre_join.mic_off')}
                        onClick={() => dispatch(setJoinWithMic(!joinWithMic))}
                     >
                        {joinWithMic ? <MicIcon /> : <MicOffIcon />}
                     </button>
                     <button
                        type="button"
                        id="pre-join-toggle-webcam"
                        className={cx(classes.toggle, !joinWithWebcam && classes.toggleOff)}
                        aria-pressed={joinWithWebcam}
                        aria-label={joinWithWebcam ? t('pre_join.webcam_on') : t('pre_join.webcam_off')}
                        title={joinWithWebcam ? t('pre_join.webcam_on') : t('pre_join.webcam_off')}
                        onClick={() => dispatch(setJoinWithWebcam(!joinWithWebcam))}
                     >
                        {joinWithWebcam ? <VideocamIcon /> : <VideocamOffIcon />}
                     </button>
                  </div>
               </div>
               {joinWithMic && microphone.stream && (
                  <Box sx={{ mt: 2 }}>
                     <Typography variant="caption" color="textSecondary">
                        {t('pre_join.mic_level')}
                     </Typography>
                     <LinearProgress
                        variant="determinate"
                        value={Math.round(level * 100)}
                        className={classes.meter}
                        color="secondary"
                        id="pre-join-mic-level"
                     />
                  </Box>
               )}
            </div>

            <div className={classes.side}>
               <Typography variant="h4" gutterBottom>
                  {t('pre_join.title')}
               </Typography>
               <Typography color="textSecondary">{t('pre_join.joining_as', { name })}</Typography>

               {denied && (
                  <Alert severity="warning" sx={{ mt: 2 }} id="pre-join-denied">
                     {t('pre_join.permission_denied')}
                  </Alert>
               )}
               {!denied && unavailable && (
                  <Alert severity="warning" sx={{ mt: 2 }}>
                     {t('pre_join.device_unavailable')}
                  </Alert>
               )}

               <div className={classes.selects}>
                  <DeviceSelector
                     devices={webcamDevices}
                     label={t('common:webcam')}
                     defaultName={t('common:webcam')}
                     selectedDevice={webcamDevice}
                     onChange={(device) => dispatch(setCurrentDevice({ device, source: 'webcam' }))}
                  />
                  <DeviceSelector
                     devices={micDevices}
                     label={t('common:microphone')}
                     defaultName={t('common:microphone')}
                     selectedDevice={micDevice}
                     onChange={(device) => dispatch(setCurrentDevice({ device, source: 'mic' }))}
                  />
               </div>

               <Button variant="contained" size="large" fullWidth onClick={handleJoin} id="pre-join-join-button">
                  {t('pre_join.join_now')}
               </Button>
               <Typography variant="caption" color="textSecondary" component="p" sx={{ mt: 1.5 }}>
                  {t('pre_join.hint')}
               </Typography>
            </div>
         </div>
      </div>
   );
}
