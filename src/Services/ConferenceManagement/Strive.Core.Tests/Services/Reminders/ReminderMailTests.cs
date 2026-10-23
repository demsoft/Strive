using System;
using Strive.Core.Services.Reminders;
using Xunit;

namespace Strive.Core.Tests.Services.Reminders
{
    public class ReminderMailTests
    {
        private static readonly DateTimeOffset Start = new(2026, 10, 24, 17, 0, 0, TimeSpan.Zero); // a Saturday

        [Fact]
        public void FormatTime_Nigeria_IsInWestAfricaTime()
        {
            Assert.Equal("Sat 24 Oct, 18:00 WAT", ReminderMail.FormatTime(Start, "NG"));
        }

        [Fact]
        public void FormatTime_OtherCountries_AreInUtc()
        {
            Assert.Equal("Sat 24 Oct, 17:00 UTC", ReminderMail.FormatTime(Start, "GH"));
        }

        [Fact]
        public void FormatTime_AnOffsetInTheInput_DoesNotChangeTheMoment()
        {
            var sameMoment = new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.FromHours(-5));

            Assert.Equal("Sat 24 Oct, 18:00 WAT", ReminderMail.FormatTime(sameMoment, "ng"));
        }

        [Fact]
        public void Build_PutsTitleHostTimeAndLinkInBothVersions_AndEscapesTheHtml()
        {
            var mail = ReminderMail.Build("Piano <b>101</b> & more", "Ada \"A\"", "Sat 24 Oct, 18:00 WAT",
                "https://gbedu.example.com/s/abcd2345");

            Assert.Equal("Starting soon: Piano <b>101</b> & more", mail.Subject);
            Assert.Contains("https://gbedu.example.com/s/abcd2345", mail.Text);
            Assert.Contains("href=\"https://gbedu.example.com/s/abcd2345\"", mail.Html);
            Assert.DoesNotContain("<b>101</b>", mail.Html);
            Assert.Contains("Piano &lt;b&gt;101&lt;/b&gt; &amp; more", mail.Html);
        }
    }
}
