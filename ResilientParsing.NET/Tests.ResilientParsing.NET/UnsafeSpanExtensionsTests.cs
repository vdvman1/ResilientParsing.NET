using ResilientParsing.NET.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Tests.ResilientParsing.NET
{
    public class UnsafeSpanExtensionsTests
    {
        public static IEnumerable<object[]> GetValueTypesArrayGenerator()
        {
            yield return new object[]
            {
                new[] { 8, 1, 6, 3, 10, 100 },
            };
        }

        [Theory]
        [MemberData(nameof(GetValueTypesArrayGenerator))]
        public void TestGetUnsafeValueTypes(int[] expected)
        {
            ReadOnlySpan<int> span = expected.AsSpan();
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], span.GetUnchecked(i));
            }
        }

        public static IEnumerable<object[]> GetReferenceTypesArrayGenerator()
        {
            yield return new object[]
            {
                new[] { "8", "1", "6", "3", "10", "100" },
            };
        }

        [Theory]
        [MemberData(nameof(GetReferenceTypesArrayGenerator))]
        public void TestGetUnsafeReferenceTypes(string[] expected)
        {
            ReadOnlySpan<string> span = expected.AsSpan();
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.True(ReferenceEquals(expected[i], span.GetUnchecked(i)));
            }
        }

        [Theory]
        [MemberData(nameof(GetValueTypesArrayGenerator))]
        public void TestAtUnsafe(int[] data)
        {
            Span<int> span = data.AsSpan();
            for (int i = 0; i < data.Length; i++)
            {
                ref int expectedRef = ref span[i];
                ref int actualRef = ref span.AtUnchecked(i);
                Assert.True(Unsafe.AreSame(ref expectedRef, ref actualRef));
                Assert.Equal(expectedRef, actualRef);
                actualRef = -60;
                Assert.Equal(expectedRef, actualRef);
            }
        }

        [Theory]
        [MemberData(nameof(GetValueTypesArrayGenerator))]
        public void TestNonNullAsSpanUnchecked(int[] data)
        {
            Span<int> expectedSpan = data.AsSpan();
            Span<int> actualSpan = data.NonNullAsSpanUnchecked();
            Assert.True(expectedSpan == actualSpan);
        }

        [Theory]
        [MemberData(nameof(GetValueTypesArrayGenerator))]
        public void TestSliceUnchecked(int[] data)
        {
            Span<int> span = data.AsSpan();
            for (int i = 0; i < data.Length; i++)
            {
                Assert.True(span.Slice(i) == span.SliceUnchecked(i));
            }
        }

        [Theory]
        [MemberData(nameof(GetValueTypesArrayGenerator))]
        public void TestSliceLengthUnchecked(int[] data)
        {
            Span<int> span = data.AsSpan();
            for (int i = 0; i < data.Length; i++)
            {
                Assert.True(span.Slice(0, i) == span.SliceLengthUnchecked(i));
            }
        }
    }
}
