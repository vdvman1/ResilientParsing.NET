using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ResilientParsing.NET.Utilities
{
    internal static class UnsafeSpanExtensions
    {
        /// <summary>
        /// Get the value at <paramref name="index"/> without doing any bounds checks
        /// </summary>
        /// <typeparam name="T">The type of items in the <see cref="ReadOnlySpan{T}"/></typeparam>
        /// <param name="span">The <see cref="ReadOnlySpan{T}"/> to fetch the value from</param>
        /// <param name="index">The zero-based index of the element to get.</param>
        /// <returns></returns>
        /// <remarks>
        /// It is the caller's responsibility to ensure the index is within the bounds of the span.
        /// Indexing outside the bounds of the span is undefined behaviour
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetUnchecked<T>(this ReadOnlySpan<T> span, int index)
        {
            Debug.Assert(span.Length > 0);

            ref T ptr = ref MemoryMarshal.GetReference(span);
            ptr = ref Unsafe.Add(ref ptr, index);
            return ptr;
        }

        /// <summary>
        /// Get a reference to the value at <paramref name="index"/> without doing any bounds checks
        /// </summary>
        /// <typeparam name="T">The type of items in the <see cref="ReadOnlySpan{T}"/></typeparam>
        /// <param name="span">The <see cref="ReadOnlySpan{T}"/> to fetch the value from</param>
        /// <param name="index">The zero-based index of the element to get.</param>
        /// <returns></returns>
        /// <remarks>
        /// It is the caller's responsibility to ensure the index is within the bounds of the span.
        /// Indexing outside the bounds of the span is undefined behaviour
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T AtUnchecked<T>(this Span<T> span, int index)
        {
            Debug.Assert(index < span.Length);

            ref T ptr = ref MemoryMarshal.GetReference(span);
            ptr = ref Unsafe.Add(ref ptr, index);
            return ref ptr;
        }

        /// <summary>
        /// Creates a new span over a target array, without doing a null check
        /// </summary>
        /// <typeparam name="T">The type of the array.</typeparam>
        /// <param name="array">The array to convert.</param>
        /// <returns>The span representation of the array.</returns>
        /// <remarks>
        /// It is the callers responsibility to ensure the array is not null.
        /// Creating a span from a null array with this method is undefined behaviour
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> NonNullAsSpanUnchecked<T>(this T[] array)
        {
            Debug.Assert(array is not null);

            return MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference(array), array.Length);
        }

        /// <summary>
        /// Forms a slice out of the current span that begins at a specified index, without doing any bounds checks
        /// </summary>
        /// <typeparam name="T">The type of items in the <see cref="Span{T}"/></typeparam>
        /// <param name="span">The <see cref="Span{T}"/> to slice</param>
        /// <param name="start">The index at which to begin the slice.</param>
        /// <returns>A span that consists of all elements of the current span from start to the end of the span.</returns>
        /// <remarks>
        /// It is the callers responsibility to ensure that <paramref name="span"/> is between 0 and the length of the span.
        /// Slicing with a start index outside the valid range is undefined behaviour.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> SliceUnchecked<T>(this Span<T> span, int start)
        {
            Debug.Assert(start <= span.Length);

            ref T ptr = ref MemoryMarshal.GetReference(span);
            ptr = ref Unsafe.Add(ref ptr, start);
            return MemoryMarshal.CreateSpan(ref ptr, span.Length - start);
        }

        /// <summary>
        /// Forms a slice out of the current span that begins at the start of the span for a specified length.
        /// </summary>
        /// <typeparam name="T">The type of items in the <see cref="Span{T}"/></typeparam>
        /// <param name="span">The <see cref="Span{T}"/> to slice</param>
        /// <param name="length">The desired length for the slice.</param>
        /// <returns>A span that consists of <paramref name="length"/> elements from the current span starting at the start of the span</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> SliceLengthUnchecked<T>(this Span<T> span, int length)
        {
            Debug.Assert(length <= span.Length);

            ref T ptr = ref MemoryMarshal.GetReference(span);
            return MemoryMarshal.CreateSpan(ref ptr, length);
        }
    }
}
