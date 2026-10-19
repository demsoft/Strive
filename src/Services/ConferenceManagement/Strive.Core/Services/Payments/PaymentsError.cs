using Strive.Core.Dto;
using Strive.Core.Errors;

namespace Strive.Core.Services.Payments
{
    public class PaymentsError : ErrorsProvider<ServiceErrorCode>
    {
        public static Error NotEnabled =>
            BadRequest("Paid sessions are not available yet.", ServiceErrorCode.Payments_NotEnabled);

        public static Error InvalidProfile(string message) =>
            BadRequest(message, ServiceErrorCode.Payments_InvalidProfile);

        public static Error InvalidAccount =>
            BadRequest("This bank account could not be found. Check the bank and the number.",
                ServiceErrorCode.Payments_InvalidAccount);

        public static Error ProviderUnavailable =>
            BadRequest("The payment service did not answer. Try again in a moment.",
                ServiceErrorCode.Payments_ProviderUnavailable);

        public static Error CreatorNotFound =>
            NotFound("There is no creator profile.", ServiceErrorCode.Payments_CreatorNotFound);
    }
}
