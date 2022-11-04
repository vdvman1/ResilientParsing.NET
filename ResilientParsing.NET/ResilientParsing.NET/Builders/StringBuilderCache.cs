using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResilientParsing.NET.Builders
{
    // Based heavily on the System.Text.StringBuilderCache internal class used by the .NET standard library
    // https://github.com/dotnet/runtime/blob/3dbc850af3e8bfd6d529ed90cf00247dc9a24512/src/libraries/Common/src/System/Text/StringBuilderCache.cs

    /// <summary>
    /// Provides a cached reusable instance of <see cref="StringBuilder"/> per thread.
    /// </summary>
    public static class StringBuilderCache
    {
        // The value 360 was chosen in discussion with performance experts as a compromise between using
        // as little memory per thread as possible and still covering a large part of short-lived
        // StringBuilder creations on the startup path of VS designers.
        public const int MaxBuilderSize = 360;
        public const int DefaultCapacity = 16; // == StringBuilder.DefaultCapacity

        [ThreadStatic]
        private static StringBuilder? t_cachedInstance;

        /// <summary>Get a StringBuilder for the specified capacity.</summary>
        /// <remarks>If a StringBuilder of an appropriate size is cached, it will be returned and the cache emptied.</remarks>
        public static StringBuilder Acquire(int capacity = DefaultCapacity)
        {
            if (capacity <= MaxBuilderSize)
            {
                StringBuilder? sb = t_cachedInstance;
                if (sb != null)
                {
                    // Avoid stringbuilder block fragmentation by getting a new StringBuilder
                    // when the requested size is larger than the current capacity
                    if (capacity <= sb.Capacity)
                    {
                        t_cachedInstance = null;
                        sb.Clear();
                        return sb;
                    }
                }
            }

            return new StringBuilder(capacity);
        }

        /// <summary>Place the specified builder in the cache if it is not too big.</summary>
        public static void Release(StringBuilder sb)
        {
            if (sb.Capacity <= MaxBuilderSize)
            {
                t_cachedInstance = sb;
            }
        }

        /// <summary>ToString() the stringbuilder, Release it to the cache, and return the resulting string.</summary>
        public static string GetStringAndRelease(StringBuilder sb)
        {
            string result = sb.ToString();
            Release(sb);
            return result;
        }
    }
}
