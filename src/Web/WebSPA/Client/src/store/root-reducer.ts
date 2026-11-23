import { combineReducers } from 'redux';
import { PersistConfig, persistReducer } from 'redux-persist';
// HRM meetings must never touch localStorage, including during dependency initialization.
const memory = new Map<string, string>();
const storage = {
   getItem: async (key: string) => memory.get(key) ?? null,
   setItem: async (key: string, value: string) => { memory.set(key, value); },
   removeItem: async (key: string) => { memory.delete(key); },
};
import auth from '../features/auth/reducer';
import breakoutRooms from '../features/breakout-rooms/reducer';
import chat from '../features/chat/reducer';
import conference from '../features/conference/reducer';
import createConference from '../features/create-conference/reducer';
import diagnostics from '../features/diagnostics/reducer';
import handRaise from '../features/hand-raise/reducer';
import equipment from '../features/equipment/reducer';
import lobby from '../features/lobby/reducer';
import media from '../features/media/reducer';
import poll from '../features/poll/reducer';
import reactions from '../features/reactions/reducer';
import recording from '../features/recording/reducer';
import latency from '../features/latency/reducer';
import metronome from '../features/metronome/reducer';
import lessonMode from '../features/lesson-mode/reducer';
import watchTogether from '../features/watch-together/reducer';
import rooms from '../features/rooms/reducer';
import scenes from '../features/scenes/reducer';
import settings from '../features/settings/reducer';
import whiteboard from '../features/whiteboard/reducer';
import signalr from './signal/reducer';

const settingsPersistConfig: PersistConfig<any> = {
   key: 'settings',
   storage: storage,
   whitelist: ['obj'],
};

const rootReducer = combineReducers({
   auth,
   conference,
   createConference,
   chat,
   signalr,
   media,
   rooms,
   settings: persistReducer(settingsPersistConfig, settings),
   equipment,
   scenes,
   breakoutRooms,
   diagnostics,
   poll,
   whiteboard,
   handRaise,
   lobby,
   recording,
   watchTogether,
   lessonMode,
   metronome,
   latency,
   reactions,
});

export default rootReducer;
