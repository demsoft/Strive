import { DomainError } from './communication-types';
import { EquipmentCommandAction } from './equipment-hub.types';
import { PollAnswer, PollConfig, PollInstruction, PollState } from './features/poll/types';
import { ProducerSource } from './store/webrtc/types';

export type SyncStatePayload = { id: string; value: any };

export type FetchChatMessagesDto = { channel: string; start: number; end: number };

export type SendChatMessageDto = {
   message: string;
   channel: string;
   options: ChatMessageOptions;
};

export type ChatMessageOptions = {
   isAnnouncement: boolean;
   isAnonymous: boolean;
};

export type ChatMessageDto = {
   id: string;
   channel: string;
   sender?: ChatMessageSender;
   message: string;
   timestamp: string;
   options: ChatMessageOptions;
};

export type ChatMessageSender = {
   participantId: string;
   meta: ParticipantMetadata;
};

export type LowerParticipantsHandDto = {
   participantId: string;
};

export type LobbyParticipantDto = {
   participantId: string;
};

export type LobbyStatus = 'waiting' | 'admitted' | 'denied';

export type LobbyStatusDto = {
   status: LobbyStatus;
};

export type SendReactionDto = {
   emoji: string;
};

export type ReactionDto = {
   participantId: string;
   emoji: string;
   timestamp: string;
};

export type SetUserIsTypingDto = {
   channel: string;
   isTyping: boolean;
};

export type ParticipantMetadata = {
   displayName: string;
};

export type RoomCreationInfo = {
   displayName: string;
};

export type SwitchRoomDto = {
   roomId: string;
};

export type OpenBreakoutRoomsDto = BreakoutRoomsConfig & {
   assignedRooms?: string[][];
};

export type BreakoutRoomsConfig = {
   deadline?: string;
   description?: string;
   amount: number;
};

export type PermissionValue = number | string | boolean;

export type Permissions = { [key: string]: PermissionValue };

export type PermissionLayer = {
   order: number;
   name: string;
   permissions: Permissions;
};

export type ParticipantPermissionInfo = {
   participantId: string;
   layers: PermissionLayer[];
};

export type SetTemporaryPermissionDto = {
   participantId: string;
   permissionKey: string;
   value?: PermissionValue;
};

export type KickParticipantRequestDto = {
   participantId: string;
};

export type IceServerInfo = {
   urls: string[];
   username?: string;
   credential?: string;
};

export type SfuConnectionInfo = {
   url: string;
   authToken: string;
   /** STUN/TURN servers (e.g. to reach the SFU from behind a strict firewall), empty if none are configured */
   iceServers: IceServerInfo[];
};

export type SendEquipmentCommandDto = {
   connectionId: string;
   source: ProducerSource;
   deviceId?: string;
   action: EquipmentCommandAction;
};

export type EquipmentErrorDto = {
   connectionId: string;
   error: DomainError;
};

export type ParticipantKickedReason = 'byModerator' | 'newSessionConnected';
export type RequestDisconnectDto = {
   reason: ParticipantKickedReason;
};

export type CreatePollDto = {
   instruction: PollInstruction;
   config: PollConfig;
   initialState: PollState;
   roomId?: string;
};

export type SubmitPollAnswerDto = {
   pollId: string;
   answer: PollAnswer;
};

export type UpdatePollStateDto = {
   pollId: string;
   state: PollState;
};

export type DeletePollDto = {
   pollId: string;
};

export type DeletePollAnswerDto = {
   pollId: string;
};
