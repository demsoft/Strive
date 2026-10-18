import { useEffect } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { RootState } from 'src/store';
import useInstrumentMonitor from 'src/store/webrtc/hooks/useInstrumentMonitor';
import { setMonitorStatus } from '../reducer';

/**
 * Keeps the monitor of the own instrument running while the conference is open, and tells the settings what it does.
 * It shows nothing itself.
 */
export default function InstrumentMonitor() {
   const dispatch = useDispatch();
   const musicMode = useSelector((state: RootState) => Boolean(state.settings.obj.mic.musicMode));
   const wanted = useSelector((state: RootState) => Boolean(state.settings.obj.mic.monitorInstrument));
   const deviceId = useSelector((state: RootState) => state.settings.obj.mic.instrumentDeviceId);

   const status = useInstrumentMonitor(musicMode && wanted, deviceId);

   useEffect(() => {
      dispatch(setMonitorStatus(status));
   }, [status.state, status.state === 'playing' ? status.delayMs : 0]);

   // gone with the conference: the settings must not show an old state
   useEffect(() => () => void dispatch(setMonitorStatus({ state: 'off' })), []);

   return null;
}
