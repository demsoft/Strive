namespace Strive.Core.Services.Payments
{
    /// <summary>
    ///     Settings for paid sessions (configuration section "Payments"). Nothing is offered while it is not enabled.
    /// </summary>
    public class PaymentsOptions
    {
        public const string Section = "Payments";

        public bool Enabled { get; set; }

        /// <summary>The share of every ticket that the platform keeps, in percent</summary>
        public int PlatformCommissionPercent { get; set; } = 10;

        /// <summary>The country and currency creators are onboarded in. Others are added one by one.</summary>
        public string Country { get; set; } = "NG";

        public string Currency { get; set; } = "NGN";

        /// <summary>
        ///     Passes that renew themselves: the card of the first payment is charged again every pass period, until the
        ///     person stops it. Off until it has been tried with test cards.
        /// </summary>
        public bool AutoRenewEnabled { get; set; }

        /// <summary>How long to wait before a renewal that did not work is tried again</summary>
        public int RenewRetryHours { get; set; } = 24;

        /// <summary>How many times a renewal is tried before the subscription ends</summary>
        public int RenewMaxAttempts { get; set; } = 3;

        /// <summary>How many days before a renewal the person is told about it by mail</summary>
        public int RenewNoticeDays { get; set; } = 3;

        public PaystackOptions Paystack { get; set; } = new();
    }

    public class PaystackOptions
    {
        /// <summary>The secret key of the Paystack account (test or live). Never in a file that is committed.</summary>
        public string? SecretKey { get; set; }

        public string BaseUrl { get; set; } = "https://api.paystack.co";
    }
}
