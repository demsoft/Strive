import { createSlice, PayloadAction } from '@reduxjs/toolkit';
import { DomainError, SuccessOrError } from 'src/communication-types';
import { getEquipmentToken } from 'src/core-hub';
import { ProducerDevice } from 'src/store/webrtc/types';
import { fetchDevices } from './thunks';
import { AnyInputDevice, InputDeviceDto } from './types';

export type StriveSettings = {
   mic: {
      device?: AnyInputDevice;
      audioGain: number;
      /** raw stereo sound for instruments: no echo cancellation, noise suppression or automatic gain */
      musicMode?: boolean;
      /** a second sound card input (piano, bass) sent as a track of its own next to the microphone */
      instrumentDeviceId?: string;
      /** the instrument is also played in the headphones of the musician himself, straight from the sound card */
      monitorInstrument?: boolean;
   };
   webcam: {
      device?: AnyInputDevice;
   };
   screen: {
      device?: AnyInputDevice;
   };
   diagnostics: {
      enableVideoOverlay: boolean;
   };
   conference: {
      playSoundOnOpen?: boolean;
      /** the microphone is turned on automatically after joining */
      joinWithMic?: boolean;
      /** the webcam is turned on automatically after joining */
      joinWithWebcam?: boolean;
   };
};

type SettingsState = {
   open: boolean;
   obj: StriveSettings;
   equipmentToken: string | null;
   equipmentTokenError: DomainError | null;
   availableDevices: InputDeviceDto[] | null;
};

const initialState: SettingsState = {
   open: false,
   obj: {
      mic: {
         audioGain: 1,
      },
      webcam: {},
      screen: {},
      diagnostics: {
         enableVideoOverlay: false,
      },
      conference: {
         playSoundOnOpen: false,
      },
   },
   equipmentToken: null,
   equipmentTokenError: null,
   availableDevices: null,
};

const settingsSlice = createSlice({
   name: 'settings',
   initialState,
   reducers: {
      openSettings(state) {
         state.open = true;
      },
      closeSettings(state) {
         state.open = false;
      },
      setAudioGain(state, { payload }: PayloadAction<number>) {
         state.obj.mic.audioGain = payload;
      },
      setInstrumentDeviceId(state, { payload }: PayloadAction<string | undefined>) {
         state.obj.mic.instrumentDeviceId = payload;
      },
      setMonitorInstrument(state, { payload }: PayloadAction<boolean>) {
         state.obj.mic.monitorInstrument = payload;
      },
      setMusicMode(state, { payload }: PayloadAction<boolean>) {
         state.obj.mic.musicMode = payload;
      },
      setCurrentDevice(
         state,
         { payload: { source, device } }: PayloadAction<{ source: ProducerDevice; device: AnyInputDevice | undefined }>,
      ) {
         state.obj[source].device = device;
      },
      setEnableVideoOverlay(state, { payload }: PayloadAction<boolean>) {
         state.obj.diagnostics.enableVideoOverlay = payload;
      },
      setPlaySoundOnOpen(state, { payload }: PayloadAction<boolean>) {
         state.obj.conference.playSoundOnOpen = payload;
      },
      setJoinWithMic(state, { payload }: PayloadAction<boolean>) {
         state.obj.conference.joinWithMic = payload;
      },
      setJoinWithWebcam(state, { payload }: PayloadAction<boolean>) {
         state.obj.conference.joinWithWebcam = payload;
      },
   },
   extraReducers: {
      [getEquipmentToken.returnAction]: (state, action: PayloadAction<SuccessOrError<string>>) => {
         if (action.payload.success) {
            state.equipmentToken = action.payload.response;
         } else {
            state.equipmentTokenError = action.payload.error;
         }
      },
      [getEquipmentToken.action]: (state) => {
         state.equipmentTokenError = null;
      },
      [fetchDevices.fulfilled.type]: (state, { payload }: PayloadAction<InputDeviceDto[]>) => {
         state.availableDevices = payload;
      },
   },
});

export const {
   closeSettings,
   openSettings,
   setAudioGain,
   setCurrentDevice,
   setEnableVideoOverlay,
   setJoinWithMic,
   setInstrumentDeviceId,
   setJoinWithWebcam,
   setMonitorInstrument,
   setMusicMode,
   setPlaySoundOnOpen,
} = settingsSlice.actions;

export default settingsSlice.reducer;
