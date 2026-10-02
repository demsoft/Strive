import { createSlice, PayloadAction } from '@reduxjs/toolkit';
import { LobbyStatus, LobbyStatusDto } from 'src/core-hub.types';
import { connectSignal, onEventOccurred } from 'src/store/signal/actions';
import { LOBBY, SynchronizedLobby } from 'src/store/signal/synchronization/synchronized-object-ids';
import { synchronizeObjectState } from 'src/store/signal/synchronized-object';
import { events } from 'src/core-hub';

export type LobbyState = {
   /** the status of this participant, null if the lobby does not apply (joined directly) */
   status: LobbyStatus | null;
   /** the participants waiting in the lobby, only synchronized for participants that may admit */
   synchronized: SynchronizedLobby | null;
};

const initialState: LobbyState = {
   status: null,
   synchronized: null,
};

const lobbySlice = createSlice({
   name: 'lobby',
   initialState,
   reducers: {},
   extraReducers: {
      [connectSignal.type]: (state) => {
         state.status = null;
         state.synchronized = null;
      },
      [onEventOccurred(events.onLobbyStatus).type]: (state, { payload }: PayloadAction<LobbyStatusDto>) => {
         state.status = payload.status;
      },
      ...synchronizeObjectState([{ type: 'single', baseId: LOBBY, propertyName: 'synchronized' }]),
   },
});

export default lobbySlice.reducer;
