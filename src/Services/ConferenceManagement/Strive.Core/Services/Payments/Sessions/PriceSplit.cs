using System;
using System.Collections.Generic;
using System.Linq;

namespace Strive.Core.Services.Payments.Sessions
{
    /// <summary>
    ///     Divides the price of a ticket: the commission of the platform first, then the rest between the creators by their
    ///     shares. Everything in whole minor units, the parts always add up to the price.
    /// </summary>
    public static class PriceSplit
    {
        public static long PlatformAmount(long priceMinor, int commissionPercent)
        {
            return (long) Math.Round(priceMinor * commissionPercent / 100m, MidpointRounding.AwayFromZero);
        }

        public static IReadOnlyList<OrderAllocation> Allocate(long priceMinor, int commissionPercent,
            IReadOnlyList<RevenueShare> shares)
        {
            if (shares.Count == 0 || shares.Sum(x => x.Percent) != 100)
                throw new ArgumentException("The shares of the creators must add up to 100%.", nameof(shares));

            var platform = PlatformAmount(priceMinor, commissionPercent);
            var rest = priceMinor - platform;

            var allocations = new List<OrderAllocation> {new(OrderAllocation.Platform, platform)};
            var given = 0L;
            for (var i = 0; i < shares.Count; i++)
            {
                // the last one gets what is left, so that rounding never loses or invents a kobo
                var amount = i == shares.Count - 1 ? rest - given : rest * shares[i].Percent / 100;
                given += amount;
                allocations.Add(new OrderAllocation(shares[i].CreatorId, amount));
            }

            return allocations;
        }
    }
}
