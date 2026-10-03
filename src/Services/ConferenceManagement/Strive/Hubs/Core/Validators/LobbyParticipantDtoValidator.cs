using FluentValidation;
using Strive.Hubs.Core.Dtos;

namespace Strive.Hubs.Core.Validators
{
    public class LobbyParticipantDtoValidator : AbstractValidator<LobbyParticipantDto>
    {
        public LobbyParticipantDtoValidator()
        {
            RuleFor(x => x.ParticipantId).NotEmpty();
        }
    }
}
