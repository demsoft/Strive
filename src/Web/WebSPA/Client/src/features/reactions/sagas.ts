import { PayloadAction } from '@reduxjs/toolkit';
import { delay, put, takeEvery } from 'redux-saga/effects';
import { events } from 'src/core-hub';
import { ReactionDto } from 'src/core-hub.types';
import { onEventOccurred } from 'src/store/signal/actions';
import { addReaction, removeReaction } from './reducer';

/** how long a reaction is displayed */
export const REACTION_DISPLAY_MS = 4000;

let nextId = 0;

export default function* mySaga() {
   yield takeEvery(onEventOccurred(events.onReaction).type, onReaction);
}

export function* onReaction({ payload }: PayloadAction<ReactionDto>) {
   const id = `${payload.participantId}-${nextId++}`;

   yield put(addReaction({ id, participantId: payload.participantId, emoji: payload.emoji }));
   yield delay(REACTION_DISPLAY_MS);
   yield put(removeReaction(id));
}
