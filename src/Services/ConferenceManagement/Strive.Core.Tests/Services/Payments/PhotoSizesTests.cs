using Strive.Core.Services.Payments;
using Xunit;

namespace Strive.Core.Tests.Services.Payments
{
    public class PhotoSizesTests
    {
        [Theory]
        [InlineData(null, 400)]
        [InlineData(0, 64)]
        [InlineData(1, 64)]
        [InlineData(64, 64)]
        [InlineData(65, 128)]
        [InlineData(129, 256)]
        [InlineData(257, 400)]
        [InlineData(100000, 400)]
        [InlineData(-5, 64)]
        public void Bucket_IsTheSmallestSizeThatIsEnough_AndNeverMoreThanTheBiggest(int? asked, int expected)
        {
            Assert.Equal(expected, PhotoSizes.Bucket(asked));
        }
    }
}
