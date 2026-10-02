using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Strive.Core.Interfaces.Gateways;

namespace Strive.Core.Services.HandRaise.Gateways
{
    public interface IHandRaiseRepository : IStateRepository
    {
        /// <summary>
        ///     Raise the hand of a participant. Keeps the time of the first call if the hand is already raised.
        /// </summary>
        /// <returns>Returns true if the hand was not raised before</returns>
        ValueTask<bool> Raise(Participant participant, DateTimeOffset raisedAt);

        /// <returns>Returns true if the hand was raised</returns>
        ValueTask<bool> Lower(Participant participant);

        ValueTask<IReadOnlyDictionary<string, DateTimeOffset>> GetAll(string conferenceId);
    }
}
