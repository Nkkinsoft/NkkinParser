using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.InteropServices;

namespace NkkinParser;

public static unsafe class TextNormalizer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Normalize(ReadOnlySpan<char> input)
    {
        if (input.IsEmpty) return string.Empty;

        // Skip leading whitespace
        int start = 0;
        while (start < input.Length && char.IsWhiteSpace(input[start])) start++;
        if (start == input.Length) return string.Empty;

        // Skip trailing whitespace
        int end = input.Length - 1;
        while (end > start && char.IsWhiteSpace(input[end])) end--;
        var sliced = input.Slice(start, end - start + 1);

        // Check if we need to collapse whitespace
        bool needsCollapse = false;
        for (int i = 0; i < sliced.Length; i++)
        {
            if (char.IsWhiteSpace(sliced[i]))
            {
                if (i + 1 < sliced.Length && char.IsWhiteSpace(sliced[i+1]))
                {
                    needsCollapse = true;
                    break;
                }
                if (sliced[i] != ' ') // Tabs, newlines, etc.
                {
                    needsCollapse = true;
                    break;
                }
            }
        }

        if (!needsCollapse) return sliced.ToString();

        // SIMD accelerated collapse
        return CollapseWhitespaceSimd(sliced);
    }

    private static string CollapseWhitespaceSimd(ReadOnlySpan<char> input)
    {
        char[]? rented = null;
        Span<char> buffer = input.Length <= 4096 ? stackalloc char[4096] : (rented = ArrayPool<char>.Shared.Rent(input.Length));
        
        try
        {
            int destIdx = 0;
            bool lastWasSpace = false;

            fixed (char* srcPtr = input)
            {
                char* cur = srcPtr;
                char* srcEnd = srcPtr + input.Length;

                if (Vector256.IsHardwareAccelerated && (srcEnd - cur) >= Vector256<ushort>.Count)
                {
                    var vSpace = Vector256.Create((ushort)' ');
                    var vTab = Vector256.Create((ushort)'\t');
                    var vCr = Vector256.Create((ushort)'\r');
                    var vLf = Vector256.Create((ushort)'\n');

                    while (cur + Vector256<ushort>.Count <= srcEnd)
                    {
                        var chunk = Vector256.Load((ushort*)cur);
                        var isWs = Vector256.Equals(chunk, vSpace) | Vector256.Equals(chunk, vTab) |
                                   Vector256.Equals(chunk, vCr) | Vector256.Equals(chunk, vLf);
                        
                        var mask = isWs.ExtractMostSignificantBits();
                        if (mask == 0) // No whitespace in this chunk, fast copy
                        {
                            for (int k = 0; k < Vector256<ushort>.Count; k++) buffer[destIdx++] = cur[k];
                            lastWasSpace = false;
                            cur += Vector256<ushort>.Count;
                        }
                        else
                        {
                            // Fallback to scalar for complex chunks to maintain collapse logic
                            break; 
                        }
                    }
                }

                while (cur < srcEnd)
                {
                    char c = *cur;
                    if (char.IsWhiteSpace(c))
                    {
                        if (!lastWasSpace)
                        {
                            buffer[destIdx++] = ' ';
                            lastWasSpace = true;
                        }
                    }
                    else
                    {
                        buffer[destIdx++] = c;
                        lastWasSpace = false;
                    }
                    cur++;
                }
            }

            return new string(buffer.Slice(0, destIdx));
        }
        finally
        {
            if (rented != null) ArrayPool<char>.Shared.Return(rented);
        }
    }
}
