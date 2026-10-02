import { Box } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import React from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { RootState } from 'src/store';
import { selectAvailableInputDevicesFactory } from '../selectors';
import { setCurrentDevice } from '../reducer';
import DeviceSelector from './DeviceSelector';
import WebcamSettingsTest from './WebcamSettingsTest';
import useWebRtcStatus from 'src/store/webrtc/hooks/useWebRtcStatus';
import ErrorWrapper from 'src/components/ErrorWrapper';
import { useTranslation } from 'react-i18next';
import useSelectorFactory from 'src/hooks/useSelectorFactory';

const useStyles = makeStyles()((theme) => ({
   root: {
      width: '100%',
      padding: theme.spacing(3),
      paddingTop: 0,
   },
   slider: {
      padding: theme.spacing(0, 2),
   },
}));

export default function WebcamSettings() {
   const { classes } = useStyles();
   const { t } = useTranslation();
   const selectedDevice = useSelector((state: RootState) => state.settings.obj.webcam.device);
   const availableDevices = useSelectorFactory(selectAvailableInputDevicesFactory, (state: RootState, selector) =>
      selector(state, 'webcam'),
   );
   const dispatch = useDispatch();
   const webrtcState = useWebRtcStatus();

   return (
      <div className={classes.root}>
         <DeviceSelector
            devices={availableDevices}
            label={t('common:webcam')}
            defaultName={t('common:webcam')}
            selectedDevice={selectedDevice}
            onChange={(device) => dispatch(setCurrentDevice({ device, source: 'webcam' }))}
         />
         <Box
            sx={{
               mt: 4,
            }}
         >
            <ErrorWrapper
               failed={webrtcState !== 'connected'}
               error={t('conference.settings.webrtc_not_connected_error')}
            >
               <WebcamSettingsTest />
            </ErrorWrapper>
         </Box>
      </div>
   );
}
