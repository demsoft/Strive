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

        public static Error InvalidSession(string message) =>
            BadRequest(message, ServiceErrorCode.Payments_InvalidSession);

        public static Error CreatorCannotSell =>
            BadRequest("Add and verify your bank account before you sell tickets.",
                ServiceErrorCode.Payments_CreatorCannotSell);

        public static Error InvalidEmail =>
            BadRequest("Enter a valid email address: the receipt of your payment is sent there.",
                ServiceErrorCode.Payments_InvalidEmail);

        public static Error SessionNotFound =>
            NotFound("This session was not found.", ServiceErrorCode.Payments_SessionNotFound);

        public static Error SessionCancelled =>
            BadRequest("This session was called off.", ServiceErrorCode.Payments_SessionCancelled);

        public static Error TicketRequired =>
            Forbidden("You need a ticket for this session.", ServiceErrorCode.Payments_TicketRequired);

        public static Error HostCannotBuy =>
            BadRequest("You host this session: you do not need a ticket.", ServiceErrorCode.Payments_HostCannotBuy);

        public static Error PassNotOffered =>
            BadRequest("This creator does not sell a pass.", ServiceErrorCode.Payments_PassNotOffered);

        public static Error OwnPass =>
            BadRequest("This is your own page: you do not need a pass.", ServiceErrorCode.Payments_HostCannotBuy);

        public static Error OrderNotFound =>
            NotFound("This payment was not found.", ServiceErrorCode.Payments_OrderNotFound);

        public static Error CreatorNotFound =>
            NotFound("There is no creator profile.", ServiceErrorCode.Payments_CreatorNotFound);
    }
}
