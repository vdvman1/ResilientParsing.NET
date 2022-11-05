using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ResilientParsing.NET.Utilities
{
    /// <summary>
    /// Memory related utilities
    /// </summary>
    public static class MemoryUtils
    {
        /// <summary>
        /// The scale factor to use when converting between binary units, e.g. KiB -> MiB = <c>KiB * <see cref="BinaryUnitScaleFactor"/></c>
        /// </summary>
        public const int BinaryUnitScaleFactor = 1024;

        /// <summary>
        /// Number of bytes in a kibibyte
        /// </summary>
        public const int BytesPerKibibyte = BinaryUnitScaleFactor;

        /// <summary>
        /// Number of bytes in a mebibyte
        /// </summary>
        public const int BytesPerMebibyte = BytesPerKibibyte * BinaryUnitScaleFactor;

        /// <summary>
        /// A recommendation for the maximum number of bytes to allocate when using <c>stackalloc</c>
        /// </summary>
        public const int RecommendedMaxStackAllocationBytes = 1 * BytesPerKibibyte;
    }

    /// <summary>
    /// Type specific memory related utilities
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static class MemoryUtils<T>
    {
        public static readonly int RecommendMaxStackAllocationLength = MemoryUtils.RecommendedMaxStackAllocationBytes / Unsafe.SizeOf<T>();
    }
}
