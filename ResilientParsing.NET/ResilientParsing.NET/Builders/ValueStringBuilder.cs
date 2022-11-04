using ResilientParsing.NET.Utilities;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ResilientParsing.NET.Builders
{
    public ref struct ValueStringBuilder
    {
        private Span<char> InitialBuffer;
        private ValueStringBuilderFallback Fallback;

        public ValueStringBuilder(Span<char> buffer)
        {
            Fallback = default;
            Length = 0;
            InitialBuffer = buffer;
        }

        public ValueStringBuilder(int capacity)
        {
            var array = ArrayPool<char>.Shared.Rent(capacity);
            InitialBuffer = array.NonNullAsSpan();
            Fallback = array;
        }

        public ValueStringBuilder(scoped ReadOnlySpan<char> value)
        {
            Length = value.Length;
            var array = ArrayPool<char>.Shared.Rent(value.Length);
            Fallback = array;
            var span = array.NonNullAsSpan();
            InitialBuffer = span;
            value.CopyTo(span);
        }

        public int Length { get; private set; }

        public int Capacity
        {
            get => Fallback.Type == FallbackType.Builder ? Fallback.Builder.Capacity : InitialBuffer.Length;
            set
            {
                if (Fallback.Type == FallbackType.Builder)
                {
                    Fallback.Builder.Capacity = value;
                }
                else if (value > InitialBuffer.Length)
                {
                    ConvertToBuilder(value);
                }
            }
        }

        public int SpaceRemainingInInitialBuffer
        {
            get
            {
                var remaining = InitialBuffer.Length - Length;
                return remaining < 0 ? 0 : remaining;
            }
        }

        [IndexerName("Chars")]
        public char this[int index]
        {
            // This is public API, so intentionally not using unsafe indexing and relying on the underlying indexer to bounds check

            get => Fallback.Type == FallbackType.Builder ? Fallback.Builder[index] : InitialBuffer[index];
            set
            {
                if (Fallback.Type == FallbackType.Builder)
                {
                    Fallback.Builder[index] = value;
                }
                else
                {
                    InitialBuffer[index] = value;
                }
            }
        }

        public void Dispose()
        {
            switch (Fallback.Type)
            {
                case FallbackType.Array:
                    ArrayPool<char>.Shared.Return(Fallback.Array);
                    Fallback = default;
                    InitialBuffer = Span<char>.Empty;
                    break;
                case FallbackType.Builder:
                    StringBuilderCache.Release(Fallback.Builder);
                    Fallback = default;
                    Debug.Assert(InitialBuffer.IsEmpty);
                    break;
            }

            Length = 0;
        }

        // TODO: Add more methods from StringBuilder

        public void Append(char c)
        {
            if (GetWriteSpanElseBuilder(1, out Span<char> buffer, out StringBuilder? builder))
            {
                buffer.AtUnchecked(0) = c;
            }
            else
            {
                builder.Append(c);
            }

            ++Length;
        }

        [SkipLocalsInit]
        public (char high, char c) Append(Rune rune)
        {
            if (rune.AsUtf16(out char high, out char c))
            {
                Append(c);
                ++Length;
                return (high, c);
            }
            
            if (GetWriteSpanElseBuilder(2, out Span<char> buffer, out StringBuilder? builder))
            {
                buffer.AtUnchecked(0) = high;
                buffer.AtUnchecked(1) = c;
            }
            else
            {
                Span<char> tempBuffer = stackalloc char[2] { high, c };
                builder.Append(tempBuffer);
            }

            Length += 2;
            return (high, c);
        }

        public void Append(scoped ReadOnlySpan<char> text)
        {
            if (text.Length == 0)
            {
                return;
            }

            if (GetWriteSpanElseBuilder(text.Length, out Span<char> buffer, out StringBuilder? builder))
            {
                // The JIT is unable to see that buffer is guaranteed to be big enough, and so doesn't remove the bounds check inside CopyTo
                // Unfortunately, it does not seem to be possible to manually call Buffer.Memmove because it is internal
                // The implementation of Buffer.Memmove is also too complex to really be worth duplicating it
                // Thankfully, the branch is a forward branch that would never be taken, so should very quickly be predicted not taken
                text.CopyTo(buffer);
            }
            else
            {
                builder.Append(text);
            }

            Length += text.Length;
        }

        public void Append(scoped ValueStringBuilder other)
        {
            if (other.GetReadSpanElseBuilder(out ReadOnlySpan<char> otherBuffer, out StringBuilder? otherBuilder))
            {
                Append(otherBuffer);
            }
            else if (GetWriteSpanElseBuilder(otherBuilder.Length, out Span<char> buffer, out StringBuilder? builder))
            {
                otherBuilder.CopyTo(0, buffer, otherBuilder.Length);
            }
            else
            {
                builder.Append(otherBuilder);
            }
        }

        private static int WriteNum(scoped Span<char> buffer, int value)
        {
            Debug.Assert(buffer.Length >= Bounds.IntMaxChars);

            int pos = 0;
            if (value < 0)
            {
                if (value == int.MinValue)
                {
                    // See comment in Append(ReadOnlySpan<char>)
                    Bounds.MinIntStr.CopyTo(buffer);
                    return Bounds.MinIntStr.Length;
                }

                buffer.AtUnchecked(0) = '-';
                value = -value;
                pos = 1;
            }

            uint uvalue = unchecked((uint)value);
            bool formatted = uvalue.TryFormat(buffer.SliceUnchecked(pos), out int charsWritten);
            Debug.Assert(formatted);

            return pos + charsWritten;
        }

        [SkipLocalsInit]
        public void Append(int value)
        {
            bool inline = Bounds.IntMaxChars <= SpaceRemainingInInitialBuffer;

            Span<char> buffer = inline
                ? InitialBuffer.SliceUnchecked(Length)
                : stackalloc char[Bounds.IntMaxChars];

            int length = WriteNum(buffer, value);

            if (inline)
            {
                Length += length;
            }
            else
            {
                Append(buffer.SliceLengthUnchecked(length));
            }
        }

        [SkipLocalsInit]
        public void AppendRange(int min, int max)
        {
            if (min == max)
            {
                Append(min);
                return;
            }

            bool inline = Bounds.RangeMaxChars <= SpaceRemainingInInitialBuffer;

            Span<char> buffer = inline
                ? InitialBuffer.SliceUnchecked(Length)
                : stackalloc char[Bounds.RangeMaxChars];

            int pos = WriteNum(buffer, min);
            buffer.AtUnchecked(pos++) = '.';
            buffer.AtUnchecked(pos++) = '.';
            pos += WriteNum(buffer.SliceUnchecked(pos), max);

            if (inline)
            {
                Length += pos;
            }
            else
            {
                Append(buffer.SliceLengthUnchecked(pos));
            }
        }

        public void Clear()
        {
            if (Fallback.Type == FallbackType.Builder && !InitialBuffer.IsEmpty)
            {
                StringBuilderCache.Release(Fallback.Builder);
                Fallback = default;
            }

            Length = 0;
        }

        public override string ToString() => GetReadSpanElseBuilder(out ReadOnlySpan<char> buffer, out StringBuilder? builder) ? buffer.ToString() : builder.ToString();

        public string ToString(Range range)
        {
            if (GetReadSpanElseBuilder(out ReadOnlySpan<char> buffer, out StringBuilder? builder))
            {
                return buffer[range].ToString();
            }

            (int start, int count) = range.GetOffsetAndLength(builder.Length);
            return builder.ToString(start, count);
        }

        public static class Bounds
        {
            public static readonly string MinIntStr = "-2147483648";
            public const int IntMaxChars = 11; // MinIntStr.Length
            public const int RangeMaxChars = 2 + (2 * IntMaxChars); // 2 for "..", and two indices

#if DEBUG
            static Bounds()
            {
                Debug.Assert(MinIntStr.Length == IntMaxChars);
            }
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool GetWriteSpanElseBuilder(int capacity, out Span<char> buffer, [NotNullWhen(false)] out StringBuilder? builder)
        {
            if (capacity <= SpaceRemainingInInitialBuffer) // Implicitly handles capacity == 0, returning true with an empty buffer
            {
                buffer = InitialBuffer.SliceUnchecked(Length);
                builder = null;
                return true;
            }

            ConvertToBuilder(capacity);
            buffer = default;
            builder = Fallback.Builder;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool GetReadSpanElseBuilder(out scoped ReadOnlySpan<char> buffer, [NotNullWhen(false)] out StringBuilder? builder)
        {
            if (Fallback.Type == FallbackType.Builder)
            {
                buffer = default;
                builder = Fallback.Builder;
                return false;
            }

            buffer = InitialBuffer;
            builder = null;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ConvertToBuilder(int minCapacity)
        {
            if (Fallback.Type == FallbackType.Builder)
            {
                return;
            }

            Debug.Assert(Length <= InitialBuffer.Length);

            var builder = StringBuilderCache.Acquire(Math.Max(minCapacity, InitialBuffer.Length));
            builder.Append(InitialBuffer.SliceLengthUnchecked(Length));

            if (Fallback.Type == FallbackType.Array)
            {
                ArrayPool<char>.Shared.Return(Fallback.Array);
                InitialBuffer = Span<char>.Empty; // InitialBuffer was a span for the rented array, must not keep it around
            }
            // if Fallback.Type == FallbackType.None, then keep the original stack buffer for use as a temporary buffer

            Fallback = builder;
        }

        private enum FallbackType
        {
            None = 0,
            Array,
            Builder
        }

        private readonly struct ValueStringBuilderFallback
        {
            private readonly object? Value;
            public readonly FallbackType Type;

            public readonly char[] Array
            {
                get
                {
                    Debug.Assert(Type == FallbackType.Array && Value is char[]);
                    return Unsafe.As<char[]>(Value!);
                }
            }

            public readonly StringBuilder Builder
            {
                get
                {
                    Debug.Assert(Type == FallbackType.Builder && Value is StringBuilder);
                    return Unsafe.As<StringBuilder>(Value!);
                }
            }

            public ValueStringBuilderFallback(char[] array)
            {
                Value = array;
                Type = FallbackType.Array;
            }

            public ValueStringBuilderFallback(StringBuilder builder)
            {
                Value = builder;
                Type = FallbackType.Builder;
            }

            public static implicit operator ValueStringBuilderFallback(char[] array) => new(array);

            public static implicit operator ValueStringBuilderFallback(StringBuilder builder) => new(builder);
        }
    }
}
