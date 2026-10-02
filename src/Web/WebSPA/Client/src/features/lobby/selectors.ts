import { createSelector } from '@reduxjs/toolkit';
import { RootState } from 'src/store';

export const selectLobbyStatus = (state: RootState) => state.lobby.status;

export const selectIsLobbyEnabled = (state: RootState) => state.conference.conferenceState?.isLobbyEnabled ?? false;

const selectWaitingMap = (state: RootState) => state.lobby.synchronized?.participants;

/** the waiting participants, the one who waits the longest first */
export const selectWaitingParticipants = createSelector(selectWaitingMap, (participants) =>
   Object.entries(participants ?? {})
      .map(([id, info]) => ({ id, ...info }))
      .sort((a, b) => new Date(a.since).getTime() - new Date(b.since).getTime()),
);
