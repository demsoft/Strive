using FluentValidation;
using Strive.Core.Services.Reactions;
using Strive.Hubs.Core.Dtos;

namespace Strive.Hubs.Core.Validators
{
    public class SendReactionDtoValidator : AbstractValidator<SendReactionDto>
    {
        public SendReactionDtoValidator()
        {
            RuleFor(x => x.Emoji).NotEmpty().Must(Reaction.IsAllowed).WithMessage("The emoji is not an allowed reaction.");
        }
    }
}
