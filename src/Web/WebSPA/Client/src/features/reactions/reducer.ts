import { createSlice, PayloadAction } from '@reduxjs/toolkit';

export type ActiveReaction = {
   /** unique id of this occurrence (one participant can send the same emoji multiple times) */
   id: string;
   participantId: string;
   emoji: string;
};

export type ReactionsState = {
   active: ActiveReaction[];
};

/** the maximum number of reactions displayed at the same time, older ones are dropped */
export const MAX_ACTIVE_REACTIONS = 30;

const initialState: ReactionsState = {
   active: [],
};

const reactionsSlice = createSlice({
   name: 'reactions',
   initialState,
   reducers: {
      addReaction(state, { payload }: PayloadAction<ActiveReaction>) {
         state.active.push(payload);
         if (state.active.length > MAX_ACTIVE_REACTIONS) {
            state.active.splice(0, state.active.length - MAX_ACTIVE_REACTIONS);
         }
      },
      removeReaction(state, { payload }: PayloadAction<string>) {
         state.active = state.active.filter((x) => x.id !== payload);
      },
   },
});

export const { addReaction, removeReaction } = reactionsSlice.actions;
export default reactionsSlice.reducer;
