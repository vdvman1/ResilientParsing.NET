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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetUnchecked<T>(this ReadOnlySpan<T> span, int index)
        {
            Debug.Assert(span.Length > 0);

            ref T ptr = ref MemoryMarshal.GetReference(span);
            ptr = ref Unsafe.Add(ref ptr, index);
            return ptr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> NonNullAsSpan<T>(this T[] array)
        {
            Debug.Assert(array is not null);

            return MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference(array), array.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> SliceLengthUnchecked<T>(this Span<T> span, int length)
        {
            Debug.Assert(length <= span.Length);

            ref T ptr = ref MemoryMarshal.GetReference(span);
            return MemoryMarshal.CreateSpan(ref ptr, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> SliceUnchecked<T>(this Span<T> span, int offset)
        {
            Debug.Assert(offset <= span.Length);

            ref T ptr = ref MemoryMarshal.GetReference(span);
            ptr = ref Unsafe.Add(ref ptr, offset);
            return MemoryMarshal.CreateSpan(ref ptr, span.Length - offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T AtUnchecked<T>(this Span<T> span, int index)
        {
            Debug.Assert(index < span.Length);

            ref T ptr = ref MemoryMarshal.GetReference(span);
            ptr = ref Unsafe.Add(ref ptr, index);
            return ref ptr;
        }
    }
}
