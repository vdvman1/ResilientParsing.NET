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
            length = 0;
            InitialBuffer = buffer;
        }

        public ValueStringBuilder(int capacity)
        {
            length = 0;
            var array = ArrayPool<char>.Shared.Rent(capacity);
            InitialBuffer = array.NonNullAsSpanUnchecked();
            Fallback = array;
        }

        public ValueStringBuilder(scoped ReadOnlySpan<char> value)
        {
            length = value.Length;
            var array = ArrayPool<char>.Shared.Rent(value.Length);
            Fallback = array;
            var span = array.NonNullAsSpanUnchecked();
            InitialBuffer = span;
            value.CopyTo(span);
        }

        private int length;
        public int Length
        {
            get => length;
            set
            {
                if (Fallback.Type == FallbackType.Builder)
                {
                    Fallback.Builder.Length = length = value;
                    return;
                }

                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Length cannot be less than zero.");
                }

                var diff = value - length;
                switch (diff)
                {
                    case 0:
                        return;
                    case < 0:
                        length = value;
                        return;
                    default:
                        InternalAppend('\0', diff);
                        return;
                }
            }
        }

        public int Capacity
        {
            get => Fallback.Type == FallbackType.Builder ? Fallback.Builder.Capacity : InitialBuffer.Length;
            set
            {
                if (value < length)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "capacity was less than the current size.");
                }

                if (Fallback.Type == FallbackType.Builder)
                {
                    var builder = Fallback.Builder;
                    if (value > InitialBuffer.Length)
                    {
                        builder.Capacity = value;
                        return;
                    }

                    // value is >= length and <= InitialBuffer.Length
                    // therefore length <= InitialBuffer.Length 
                    builder.CopyTo(0, InitialBuffer, Math.Min(value, length));
                    ReleaseBuilder();
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
                var remaining = InitialBuffer.Length - length;
                return remaining < 0 ? 0 : remaining;
            }
        }

        [IndexerName("Chars")]
        public char this[int index]
        {
            get
            {
                if ((uint)index >= (uint)length)
                {
                    throw new IndexOutOfRangeException();
                }

                return Fallback.Type == FallbackType.Builder ? Fallback.Builder[index] : InitialBuffer[index];
            }
            set
            {
                if ((uint)index >= (uint)length)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), "Index was out of range. Must be non-negative and less than the size of the collection.");
                }

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
                    ReleaseBuilder();
                    break;
            }

            length = 0;
        }

        // TODO: Add more methods from StringBuilder

        public void Append(char value)
        {
            if (GetWriteSpanElseBuilder(1, out Span<char> buffer, out StringBuilder? builder))
            {
                buffer.AtUnchecked(0) = value;
            }
            else
            {
                builder.Append(value);
            }

            ++length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InternalAppend(char value, int repeatCount)
        {
            Debug.Assert(repeatCount > 0);

            if (GetWriteSpanElseBuilder(repeatCount, out Span<char> buffer, out StringBuilder? builder))
            {
                buffer.SliceLengthUnchecked(repeatCount).Fill(value);
            }
            else
            {
                builder.Append(value, repeatCount);
            }

            length += repeatCount;
        }

        public void Append(char value, int repeatCount)
        {
            switch (repeatCount)
            {
                case < 0:
                    throw new ArgumentOutOfRangeException(nameof(repeatCount), "Count cannot be less than zero.");
                case 0:
                    return;
            }

            InternalAppend(value, repeatCount);
        }

        [SkipLocalsInit]
        public (char high, char c) Append(Rune rune)
        {
            if (rune.AsUtf16(out char high, out char c))
            {
                Append(c);
                ++length;
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

            length += 2;
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

            length += text.Length;
        }

        public void Append(scoped ValueStringBuilder other)
        {
            if (other.GetReadSpanElseBuilder(out ReadOnlySpan<char> otherBuffer, out StringBuilder? otherBuilder))
            {
                Append(otherBuffer);
                return;
            }

            var len = otherBuilder.Length;
            if (GetWriteSpanElseBuilder(len, out Span<char> buffer, out StringBuilder? builder))
            {
                otherBuilder.CopyTo(0, buffer, len);
            }
            else
            {
                builder.Append(otherBuilder);
            }

            length += len;
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

            int pos = WriteNum(buffer, value);

            if (inline)
            {
                length += pos;
            }
            else
            {
                Append(buffer.SliceLengthUnchecked(pos));
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
                ? InitialBuffer.SliceUnchecked(length)
                : stackalloc char[Bounds.RangeMaxChars];

            int pos = WriteNum(buffer, min);
            buffer.AtUnchecked(pos++) = '.';
            buffer.AtUnchecked(pos++) = '.';
            pos += WriteNum(buffer.SliceUnchecked(pos), max);

            if (inline)
            {
                length += pos;
            }
            else
            {
                Append(buffer.SliceLengthUnchecked(pos));
            }
        }

        public void Clear() => length = 0;

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
                buffer = InitialBuffer.SliceUnchecked(length);
                builder = null;
                return true;
            }

            ConvertToBuilder(capacity);
            buffer = default;
            builder = Fallback.Builder;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool GetReadSpanElseBuilder(scoped out ReadOnlySpan<char> buffer, [NotNullWhen(false)] out StringBuilder? builder)
        {
            if (Fallback.Type == FallbackType.Builder)
            {
                buffer = default;
                builder = Fallback.Builder;
                return false;
            }

            buffer = InitialBuffer.SliceLengthUnchecked(Length);
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

            Debug.Assert(length <= InitialBuffer.Length);

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReleaseBuilder()
        {
            Debug.Assert(Fallback.Type == FallbackType.Builder);

            StringBuilderCache.Release(Fallback.Builder);
            Fallback = default;
        }
    }
}
