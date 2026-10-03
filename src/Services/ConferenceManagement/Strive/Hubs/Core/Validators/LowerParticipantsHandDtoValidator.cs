using FluentValidation;
using Strive.Hubs.Core.Dtos;

namespace Strive.Hubs.Core.Validators
{
    public class LowerParticipantsHandDtoValidator : AbstractValidator<LowerParticipantsHandDto>
    {
        public LowerParticipantsHandDtoValidator()
        {
            RuleFor(x => x.ParticipantId).NotEmpty();
        }
    }
}
