using ResilientParsing.NET.Builders;
using ResilientParsing.NET.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.ResilientParsing.NET
{
    public class MemoryUtilsTests
    {
        [Fact]
        public void KibibytesIsCorrectSize()
        {
            Assert.Equal(1024, MemoryUtils.BytesPerKibibyte);
        }

        [Fact]
        public void MebibytesIsCorrectSize()
        {
            Assert.Equal(1048576, MemoryUtils.BytesPerMebibyte);
        }

        // TODO: How to test MemoryUtils<T>.RecommendMaxStackAllocationLength?
        // The value of MemoryUtils.RecommendedMaxStackAllocationBytes may change in future, so can't hardcode the lengths
        // Calculating the lengths would be done the same as the implementation being tested, so would always pass
    }
}
