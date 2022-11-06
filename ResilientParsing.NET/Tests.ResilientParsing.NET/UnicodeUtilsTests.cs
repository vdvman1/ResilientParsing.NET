using ResilientParsing.NET.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Tests.ResilientParsing.NET
{
    public class UnicodeUtilsTests
    {
        [Theory]
        [InlineData("\0", true, '\0', '\0')]
        [InlineData("a", true, '\0', 'a')]
        [InlineData("\uFFFF", true, '\0', '\uFFFF')]
        [InlineData("\U00010000", false, '\uD800', '\uDC00')]
        [InlineData("\U000101A0", false, '\uD800', '\uDDA0')]
        [InlineData("\U0010FFFF", false, '\uDBFF', '\uDFFF')]
        public void TestAsUtf16(string text, bool result, char high, char c)
        {
            var rune = Rune.GetRuneAt(text, 0);
            Assert.Equal(result, rune.AsUtf16(out char actualHigh, out char actualC));
            Assert.Equal(high, actualHigh);
            Assert.Equal(c, actualC);
        }
    }
}
