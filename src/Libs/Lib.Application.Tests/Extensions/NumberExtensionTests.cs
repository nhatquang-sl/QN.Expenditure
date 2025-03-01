using Lib.Application.Extensions;

namespace Lib.Application.Tests.Extensions
{
    public class NumberExtensionTests
    {
        [Theory]
        [InlineData(0.0001234, 0.00012)]
        [InlineData(123.4567, 123.45)]
        [InlineData(0.00987, 0.0098)]
        public void Two_Fixed_Places(decimal number, decimal expected)
        {
            Assert.Equal(expected, number.FixedNumber(2));
        }

        [Theory]
        [InlineData(0.00012341, 0.0001234)]
        [InlineData(123.45675, 123.4567)]
        [InlineData(0.009876, 0.009876)]
        public void Four_Fixed_Places(decimal number, decimal expected)
        {
            Assert.Equal(expected, number.FixedNumber());
        }
    }
}