#nullable enable
using System.Net;

namespace Identity.API.Accounts
{
    /// <summary>The name and the button colours of the brand a mail speaks for</summary>
    public record MailBrand(string Name, string Button, string ButtonText)
    {
        public static readonly MailBrand Default = new("GOS Meet", "#7c5cff", "#fff");
        public static readonly MailBrand Gbedu = new("Gbedu", "#ff9f1c", "#2a0610");
        public static readonly MailBrand Arena = new("Arena", "#22c55e", "#07100b");

        /// <summary>The brand of the address the person is on (see <see cref="BrandInfo" />)</summary>
        public static MailBrand Of(BrandInfo? brand) =>
            brand == null ? Default : brand.IsArena ? Arena : brand.IsGbedu ? Gbedu : Default;
    }

    /// <summary>The emails of the account flows, in the colors of the brand.</summary>
    public static class EmailTemplates
    {
        public static EmailMessage ConfirmEmail(string to, string name, string link, MailBrand? brand = null) =>
            Build(brand, to, $"Confirm your email for {NameOf(brand)}", $"Welcome to {NameOf(brand)}, {name}!",
                "Confirm your email address to finish creating your account.", "Confirm email", link,
                "The link works for 24 hours. If you did not create an account you can ignore this email.");

        public static EmailMessage ResetPassword(string to, string link, MailBrand? brand = null) =>
            Build(brand, to, $"Reset your {NameOf(brand)} password", "Reset your password",
                "Somebody (hopefully you) asked to reset the password of this account.", "Choose a new password", link,
                "The link works for one hour and only once. If you did not ask for it you can ignore this email.");

        public static EmailMessage AlreadyRegistered(string to, string signInLink, string resetLink,
            MailBrand? brand = null) =>
            Build(brand, to, $"You already have a {NameOf(brand)} account", "You already have an account",
                "Somebody tried to create an account with this email address, but you have one already.",
                "Sign in", signInLink, $"Forgot your password? Reset it here: {resetLink}");

        private static string NameOf(MailBrand? brand) => brand?.Name ?? MailBrand.Default.Name;

        private static EmailMessage Build(MailBrand? brand, string to, string subject, string title, string text,
            string button, string link, string footer)
        {
            var plain = $"{title}\n\n{text}\n\n{button}: {link}\n\n{footer}\n";
            var html = $@"<!doctype html><html><body style=""margin:0;background:#0b0d17;font-family:Arial,sans-serif;color:#f3f4fa"">
<table width=""100%"" cellpadding=""0"" cellspacing=""0""><tr><td align=""center"" style=""padding:32px 16px"">
<table width=""480"" cellpadding=""0"" cellspacing=""0"" style=""max-width:480px;background:#141829;border-radius:20px;padding:32px"">
<tr><td style=""font-size:22px;font-weight:800"">{WebUtility.HtmlEncode(title)}</td></tr>
<tr><td style=""padding:16px 0 24px;line-height:1.5;color:#c9cbe0"">{WebUtility.HtmlEncode(text)}</td></tr>
<tr><td><a href=""{WebUtility.HtmlEncode(link)}"" style=""display:inline-block;padding:13px 24px;border-radius:12px;background:{(brand ?? MailBrand.Default).Button};color:{(brand ?? MailBrand.Default).ButtonText};font-weight:bold;text-decoration:none"">{WebUtility.HtmlEncode(button)}</a></td></tr>
<tr><td style=""padding-top:24px;font-size:13px;line-height:1.5;color:#8f92ad"">{WebUtility.HtmlEncode(footer)}</td></tr>
</table></td></tr></table></body></html>";
            // the main brand keeps the sender of the settings, the other brands sign with their own name
            return new EmailMessage(to, subject, plain, html,
                brand == null || brand == MailBrand.Default ? null : brand.Name);
        }
    }
}
