#nullable enable
using System;
using System.Collections.Generic;

namespace Identity.API.Accounts
{
    public record SignupsPerDay(string Date, int Count);

    public record AccountStats(int TotalUsers, int ConfirmedUsers, int WithPassword, int WithGoogle,
        IReadOnlyList<SignupsPerDay> Signups, DateTimeOffset GeneratedAt);
}
