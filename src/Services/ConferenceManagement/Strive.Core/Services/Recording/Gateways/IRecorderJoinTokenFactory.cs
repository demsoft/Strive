using System;

namespace Strive.Core.Services.Recording.Gateways
{
    /// <summary>
    ///     Creates the token a recorder uses to join a conference as a hidden, receive-only participant
    /// </summary>
    public interface IRecorderJoinTokenFactory
    {
        string Create(string recordingId, string conferenceId, TimeSpan validFor);
    }
}
