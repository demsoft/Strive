namespace Strive.Core.Services.Reminders
{
    /// <summary>
    ///     Settings for the mails that remind ticket holders of a session that is about to start (configuration section
    ///     "Reminders" and "Email"). Without a mail key no reminder is sent and none is used up.
    /// </summary>
    public class RemindersOptions
    {
        public const string Section = "Reminders";

        public bool Enabled { get; set; } = true;

        /// <summary>How long before the start the mail is sent</summary>
        public int LeadMinutes { get; set; } = 60;

        /// <summary>The address the link in the mail starts with, like https://gbedu.example.com</summary>
        public string LinkBaseUrl { get; set; } = "";

        /// <summary>A follower who asked for mail gets at most one in this many hours (batches of posts are one mail)</summary>
        public int FollowerNoticeCooldownHours { get; set; } = 6;

        /// <summary>The most people one post is mailed to</summary>
        public int FollowerNoticeMaxRecipients { get; set; } = 300;
    }

    public class EmailOptions
    {
        public const string Section = "Email";

        /// <summary>The key of the transactional API of Brevo (https://developers.brevo.com). Never in a committed file.</summary>
        public string? BrevoApiKey { get; set; }

        /// <summary>The sender, "Name &lt;address&gt;". The address must be validated at Brevo.</summary>
        public string From { get; set; } = "Gbedu <no-reply@localhost>";
    }
}
