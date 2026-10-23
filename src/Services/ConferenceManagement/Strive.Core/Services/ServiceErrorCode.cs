// ReSharper disable InconsistentNaming

namespace Strive.Core.Services
{
    public enum ServiceErrorCode
    {
        // 0-1000 reserved for UI

        // 1001- 2000 reserved for SFU

        // Common
        PermissionDenied,


        // Conference
        Conference_NotFound,
        Conference_UnexpectedError,
        Conference_NotOpen,
        Conference_InternalServiceError,
        Conference_RoomNotFound,

        // Chat
        Chat_InvalidChannel,
        Chat_PrivateMessagesDisabled,

        // Reactions
        Reactions_InvalidEmoji,
        Reactions_RateLimited,

        // Lobby
        Lobby_ParticipantNotWaiting,

        // Recording
        Recording_NotEnabled,
        Recording_AlreadyRecording,
        Recording_NotRecording,
        Recording_StartFailed,
        Recording_NotFound,

        // Watch together
        WatchTogether_InvalidVideoUrl,
        WatchTogether_NothingPlaying,
        WatchTogether_InvalidControl,

        // Metronome
        Metronome_InvalidTempo,

        // Payments
        Payments_NotEnabled,
        Payments_InvalidProfile,
        Payments_InvalidAccount,
        Payments_ProviderUnavailable,
        Payments_CreatorNotFound,
        Payments_CreatorCannotSell,
        Payments_InvalidSession,
        Payments_InvalidEmail,
        Payments_SessionNotFound,
        Payments_SessionCancelled,
        Payments_TicketRequired,
        Payments_HostCannotBuy,
        Payments_OrderNotFound,
        Payments_PassNotOffered,
        Payments_InvalidSlug,
        Payments_InvalidPhoto,
        Videos_NotEnabled,
        Videos_Invalid,
        Videos_NotFound,
        Videos_UploadNotFound,
        Videos_HasBuyers,
        Videos_NotForSale,
        Videos_InvalidReport,
        Social_NotFound,
        Social_Invalid,
        Social_InvalidComment,
        Social_CannotComment,
        Social_TooFast,
        Social_CommentNotFound,
        Payments_SubscriptionNotFound,
        Payments_SlugTaken,

        // Permissions
        Permissions_PermissionKeyNotFound,
        Permissions_InvalidPermissionValueType,

        // Equipment
        Equipment_InvalidToken,
        Equipment_NotInitialized,
        Equipment_ParticipantNotJoined,

        // Scenes
        Scenes_HasSpeaker,

        // Breakout Rooms
        BreakoutRoom_AlreadyOpen,
        BreakoutRoom_NotOpen,
        BreakoutRoom_AssigningParticipantsFailed,

        // Poll
        Poll_NotFound,
        AnswerValidationFailed,
        Poll_AnswerCannotBeChanged,
        Poll_Closed,

        // Whiteboard
        Whiteboard_NotFound,
        Whiteboard_UndoNotAvailable,
        Whiteboard_RedoNotAvailable,
        Whiteboard_WhiteboardActionHadNoEffect,
    }
}
