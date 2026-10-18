import { Alert, Box, FormControlLabel, MenuItem, Slider, Switch, TextField, Typography } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { useDispatch, useSelector } from 'react-redux';
import ErrorWrapper from 'src/components/ErrorWrapper';
import useSelectorFactory from 'src/hooks/useSelectorFactory';
import { RootState } from 'src/store';
import useWebRtcStatus from 'src/store/webrtc/hooks/useWebRtcStatus';
import { setAudioGain, setCurrentDevice, setInstrumentDeviceId, setMonitorInstrument, setMusicMode } from '../reducer';
import { selectAvailableInputDevicesFactory } from '../selectors';
import AudioSettingsTest from './AudioSettingsTest';
import DeviceSelector from './DeviceSelector';
import InstrumentLevelMeter from './InstrumentLevelMeter';

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

const marks: { value: number; label: string }[] = [
   { value: 1, label: '0%' },
   { value: 0, label: '-100%' },
   { value: 2, label: '100%' },
   { value: 3, label: '200%' },
];

export default function AudioSettings() {
   const { classes } = useStyles();
   const { t } = useTranslation();
   const audioGain = useSelector((state: RootState) => state.settings.obj.mic.audioGain);
   const audioDevice = useSelector((state: RootState) => state.settings.obj.mic.device);
   const musicMode = useSelector((state: RootState) => Boolean(state.settings.obj.mic.musicMode));
   const monitorStatus = useSelector((state: RootState) => state.settings.monitorStatus);
   const monitorInstrument = useSelector((state: RootState) => Boolean(state.settings.obj.mic.monitorInstrument));
   const instrumentDeviceId = useSelector((state: RootState) => state.settings.obj.mic.instrumentDeviceId);
   const dispatch = useDispatch();

   const handleChangeGain = (_: Event, value: number | number[]) => {
      dispatch(setAudioGain(value as number));
   };

   const audioDevices = useSelectorFactory(selectAvailableInputDevicesFactory, (state: RootState, selector) =>
      selector(state, 'mic'),
   );
   const webrtcState = useWebRtcStatus();

   return (
      <div className={classes.root}>
         <DeviceSelector
            devices={audioDevices}
            label={t('common:microphone')}
            defaultName={t('common:microphone')}
            selectedDevice={audioDevice}
            onChange={(device) => dispatch(setCurrentDevice({ device, source: 'mic' }))}
         />
         <Box sx={{ mt: 3 }}>
            <FormControlLabel
               control={<Switch checked={musicMode} onChange={(_, checked) => dispatch(setMusicMode(checked))} />}
               label={t('conference.settings.audio.music_mode')}
            />
            <Typography variant="body2" color="text.secondary">
               {t('conference.settings.audio.music_mode_description')}
            </Typography>
            {musicMode && (
               <Alert severity="info" sx={{ mt: 1 }}>
                  {t('conference.settings.audio.music_mode_hint')}
               </Alert>
            )}
            {musicMode && (
               <TextField
                  select
                  fullWidth
                  size="small"
                  sx={{ mt: 2, maxWidth: 400 }}
                  label={t('conference.settings.audio.instrument_input')}
                  helperText={t('conference.settings.audio.instrument_input_description')}
                  value={instrumentDeviceId ?? ''}
                  onChange={(e) => dispatch(setInstrumentDeviceId(e.target.value || undefined))}
               >
                  <MenuItem value="">{t('conference.settings.audio.instrument_none')}</MenuItem>
                  {audioDevices
                     .filter((x) => x.type === 'local')
                     .flatMap((x) => x.devices)
                     .map((x) => (
                        <MenuItem key={x.device.deviceId} value={x.device.deviceId}>
                           {x.label ?? x.device.deviceId}
                        </MenuItem>
                     ))}
               </TextField>
            )}
            {musicMode && instrumentDeviceId && <InstrumentLevelMeter deviceId={instrumentDeviceId} />}
            {musicMode && (
               <Box sx={{ mt: 1 }}>
                  <FormControlLabel
                     control={
                        <Switch
                           checked={monitorInstrument}
                           onChange={(_, checked) => dispatch(setMonitorInstrument(checked))}
                        />
                     }
                     label={t('conference.settings.audio.instrument_monitor')}
                  />
                  <Typography variant="body2" color="text.secondary">
                     {t('conference.settings.audio.instrument_monitor_description')}
                  </Typography>
                  {monitorInstrument && (
                     <Alert
                        id="instrument-monitor-status"
                        severity={
                           monitorStatus.state === 'playing'
                              ? 'success'
                              : monitorStatus.state === 'error'
                              ? 'error'
                              : 'info'
                        }
                        sx={{ mt: 1 }}
                     >
                        {!instrumentDeviceId
                           ? t('conference.settings.audio.instrument_monitor_no_input')
                           : monitorStatus.state === 'playing'
                           ? t('conference.settings.audio.instrument_monitor_playing', { delay: monitorStatus.delayMs })
                           : monitorStatus.state === 'blocked'
                           ? t('conference.settings.audio.instrument_monitor_blocked')
                           : monitorStatus.state === 'error'
                           ? t('conference.settings.audio.instrument_monitor_error')
                           : t('conference.settings.audio.instrument_monitor_starting')}
                     </Alert>
                  )}
               </Box>
            )}
         </Box>
         <Box
            sx={{
               mt: 4,
            }}
         >
            <Typography variant="h6" gutterBottom>
               {t('conference.settings.audio.gain')}
            </Typography>
            <div className={classes.slider}>
               <Slider
                  value={audioGain}
                  min={0}
                  max={3}
                  onChange={handleChangeGain}
                  step={0.2}
                  valueLabelDisplay="auto"
                  marks={marks}
                  valueLabelFormat={(val) => `${Math.round((val - 1) * 100)}%`}
               />
            </div>
         </Box>
         <Box
            sx={{
               mt: 4,
            }}
         >
            <ErrorWrapper
               failed={webrtcState !== 'connected'}
               error={t('conference.settings.webrtc_not_connected_error')}
            >
               <AudioSettingsTest />
            </ErrorWrapper>
         </Box>
      </div>
   );
}
