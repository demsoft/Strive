import { createSelector } from '@reduxjs/toolkit';
import { RootState } from 'src/store';
import { selectMyParticipantId } from '../auth/selectors';

export const selectRaisedHands = (state: RootState) => state.handRaise.synchronized?.raised;

/** The participant ids with a raised hand, the one who raised first at the beginning. */
export const selectRaisedHandsOrdered = createSelector(selectRaisedHands, (raised) =>
   Object.entries(raised ?? {})
      .sort(([, a], [, b]) => new Date(a).getTime() - new Date(b).getTime())
      .map(([participantId]) => participantId),
);

export const selectIsHandRaised = (state: RootState, participantId: string | null | undefined) =>
   participantId != null && selectRaisedHands(state)?.[participantId] !== undefined;

export const selectIsMyHandRaised = (state: RootState) => selectIsHandRaised(state, selectMyParticipantId(state));
