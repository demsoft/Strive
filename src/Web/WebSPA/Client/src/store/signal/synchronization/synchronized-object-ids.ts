import { Permissions } from 'src/core-hub.types';
import { ParticipantData } from 'src/features/conference/types';
import { SceneOptions } from 'src/features/create-conference/types';
import { EquipmentConnection } from 'src/features/media/types';

export const ROOMS = 'rooms';
export const CONFERENCE = 'conference';
export const PARTICIPANTS = 'participants';
export const PARTICIPANT_PERMISSIONS = 'participantPermissions';
export const CHAT = 'chat';
export const SUBSCRIPTIONS = 'subscriptions';
export const BREAKOUT_ROOMS = 'breakoutRooms';
export const MEDIA = 'media';
export const EQUIPMENT = 'equipment';
export const SCENE = 'scene';
export const SCENE_TALKINGSTICK = 'scene_talkingStick';
export const TEMPORARY_PERMISSIONS = 'temporaryPermissions';
export const POLL = 'poll';
export const POLL_RESULT = 'poll_result';
export const POLL_ANSWERS = 'poll_answers';
export const WHITEBOARDS = 'whiteboards';
export const HAND_RAISES = 'handRaises';
export const LOBBY = 'lobby';
export const RECORDING = 'recording';

/** the server serializes enums in camel case */
export type RecordingStatus = 'starting' | 'recording' | 'finalizing' | 'ready' | 'failed';

export type SynchronizedRecording = {
   active: { recordingId: string; status: RecordingStatus; startedAt: string; startedBy: string } | null;
};

export type SynchronizedLobby = {
   participants: { [participantId: string]: { displayName: string; since: string } };
};

export type SynchronizedHandRaises = {
   /** participant id -> iso timestamp of when the hand was raised */
   raised: { [participantId: string]: string };
};

export type SynchronizedParticipants = {
   participants: { [participantId: string]: ParticipantData };
};

export type SynchronizedConferenceInfo = {
   isOpen: boolean;
   moderators: string[];
   scheduledDate?: string | null;
   name: string | null;
   isPrivateChatEnabled: boolean;
   sceneOptions: SceneOptions;
   isLobbyEnabled: boolean;
   isRecordingEnabled: boolean;
};

export type SynchronizedParticipantsPermissions = {
   permissions: Permissions;
};

export type SynchronizedRooms = {
   rooms: Room[];
   defaultRoomId: string;
   participants: { [participanId: string]: string };
};

export type Room = {
   roomId: string;
   displayName: string;
};

export type ChatSynchronizedObject = {
   participantsTyping: { [id: string]: boolean };
};

export type SynchronizedEquipment = {
   connections: Record<string, EquipmentConnection>;
};
