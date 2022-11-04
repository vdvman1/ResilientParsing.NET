using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ResilientParsing.NET.Utilities
{
    /// <summary>
    /// Various utility and extension methods for working with unicode
    /// </summary>
    public static class UnicodeUtils
    {
        /// <summary>
        /// Extract the 1 or 2 <see cref="char"/> values needed to represent the codepoint represented by <paramref name="rune"/>
        /// </summary>
        /// <param name="rune">The codepoint to convert</param>
        /// <param name="high">The high surrogate if <paramref name="rune"/> is not in the BMP. Otherwise <c>'\0'</c> </param>
        /// <param name="c">The singular <see cref="char"/> if <paramref name="rune"/> is in the BMP. Otherwise the low surrogate</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AsUtf16(this Rune rune, out char high, out char c)
        {
            var value = unchecked((uint)rune.Value);
            if (value <= char.MaxValue)
            {
                high = '\0';
                c = unchecked((char)value);
                return true;
            }

            // This calculation comes from the Unicode specification, Table 3-5.
            high = (char)((value + ((0xD800u - 0x40u) << 10)) >> 10);
            c = (char)((value & 0x3FFu) + 0xDC00u);

            return false;
        }
    }
}
