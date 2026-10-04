#nullable enable
using System.Net;

namespace Identity.API.Accounts
{
    /// <summary>The emails of the account flows, in the colors of the app.</summary>
    public static class EmailTemplates
    {
        public static EmailMessage ConfirmEmail(string to, string name, string link) =>
            Build(to, "Confirm your email for Strive", $"Welcome to Strive, {name}!",
                "Confirm your email address to finish creating your account.", "Confirm email", link,
                "The link works for 24 hours. If you did not create an account you can ignore this email.");

        public static EmailMessage ResetPassword(string to, string link) =>
            Build(to, "Reset your Strive password", "Reset your password",
                "Somebody (hopefully you) asked to reset the password of this account.", "Choose a new password", link,
                "The link works for one hour and only once. If you did not ask for it you can ignore this email.");

        public static EmailMessage AlreadyRegistered(string to, string signInLink, string resetLink) =>
            Build(to, "You already have a Strive account", "You already have an account",
                "Somebody tried to create an account with this email address, but you have one already.",
                "Sign in", signInLink, $"Forgot your password? Reset it here: {resetLink}");

        private static EmailMessage Build(string to, string subject, string title, string text, string button,
            string link, string footer)
        {
            var plain = $"{title}\n\n{text}\n\n{button}: {link}\n\n{footer}\n";
            var html = $@"<!doctype html><html><body style=""margin:0;background:#0b0d17;font-family:Arial,sans-serif;color:#f3f4fa"">
<table width=""100%"" cellpadding=""0"" cellspacing=""0""><tr><td align=""center"" style=""padding:32px 16px"">
<table width=""480"" cellpadding=""0"" cellspacing=""0"" style=""max-width:480px;background:#141829;border-radius:20px;padding:32px"">
<tr><td style=""font-size:22px;font-weight:800"">{WebUtility.HtmlEncode(title)}</td></tr>
<tr><td style=""padding:16px 0 24px;line-height:1.5;color:#c9cbe0"">{WebUtility.HtmlEncode(text)}</td></tr>
<tr><td><a href=""{WebUtility.HtmlEncode(link)}"" style=""display:inline-block;padding:13px 24px;border-radius:12px;background:#7c5cff;color:#fff;font-weight:bold;text-decoration:none"">{WebUtility.HtmlEncode(button)}</a></td></tr>
<tr><td style=""padding-top:24px;font-size:13px;line-height:1.5;color:#8f92ad"">{WebUtility.HtmlEncode(footer)}</td></tr>
</table></td></tr></table></body></html>";
            return new EmailMessage(to, subject, plain, html);
        }
    }
}
