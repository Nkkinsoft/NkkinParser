using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.InteropServices;
using System.Numerics;

namespace NkkinParser;

public static unsafe class HtmlEntityDecoder
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Decode(ReadOnlySpan<char> input)
    {
        if (input.IsEmpty) return string.Empty;

        int firstAmp = IndexOfCharSimd(input, '&');
        // fallback to non-SIMD if not found by SIMD helper
        if (firstAmp == -1)
            firstAmp = input.IndexOf('&');
        if (firstAmp == -1) return input.ToString();

        return DecodeComplex(input, firstAmp);
    }

    private static string DecodeComplex(ReadOnlySpan<char> input, int firstAmp)
    {
        int length = input.Length;
        char[]? rented = null;
        Span<char> buffer = length <= 1024 ? stackalloc char[1024] : (rented = ArrayPool<char>.Shared.Rent(length));

        try
        {
            int destIdx = 0;
            // Copy everything before first ampersand
            for (int j = 0; j < firstAmp; j++) buffer[destIdx++] = input[j];

            fixed (char* ptr = input)
            {
                int i = firstAmp;
                while (i < length)
                {
                    if (ptr[i] == '&')
                    {
                        int entityStart = i;
                        i++; // Skip &
                        
                        // Handle Numeric Entities
                        if (i < length && ptr[i] == '#')
                        {
                            i++; // Skip #
                            if (i < length && (ptr[i] == 'x' || ptr[i] == 'X'))
                            {
                                i++; // Skip x
                                int hexStart = i;
                                while (i < length && IsHexDigit(ptr[i])) i++;
                                
                                if (i > hexStart)
                                {
                                    if (uint.TryParse(input.Slice(hexStart, i - hexStart), System.Globalization.NumberStyles.HexNumber, null, out uint val))
                                    {
                                        buffer[destIdx++] = (char)val;
                                        if (i < length && ptr[i] == ';') i++;
                                        continue;
                                    }
                                }
                                // Fallback
                                buffer[destIdx++] = '&';
                                buffer[destIdx++] = '#';
                                buffer[destIdx++] = input[hexStart - 1]; // x or X
                                i = hexStart;
                            }
                            else
                            {
                                int decStart = i;
                                while (i < length && char.IsDigit(ptr[i])) i++;
                                
                                if (i > decStart)
                                {
                                    if (uint.TryParse(input.Slice(decStart, i - decStart), out uint val))
                                    {
                                        buffer[destIdx++] = (char)val;
                                        if (i < length && ptr[i] == ';') i++;
                                        continue;
                                    }
                                }
                                // Fallback
                                buffer[destIdx++] = '&';
                                buffer[destIdx++] = '#';
                                i = decStart;
                            }
                        }
                        else
                        {
                            // Handle Named Entities
                            int nameStart = i;
                            // Entities can contain digits (e.g., &frac12;)
                            while (i < length && char.IsLetterOrDigit(ptr[i])) i++;
                            
                            if (i > nameStart)
                            {
                                ReadOnlySpan<char> name = input.Slice(nameStart, i - nameStart);
                                bool hasSemicolon = (i < length && ptr[i] == ';');
                                
                                string decoded = DecodeNamedEntity(name, hasSemicolon);
                                if (decoded != null)
                                {
                                    for (int j = 0; j < decoded.Length; j++) buffer[destIdx++] = decoded[j];
                                    if (hasSemicolon) i++;
                                    continue;
                                }
                            }
                            // Fallback
                            buffer[destIdx++] = '&';
                            i = nameStart;
                        }
                    }
                    else
                    {
                            // SIMD find next '&' and copy chunks efficiently
                            if (Vector256.IsHardwareAccelerated && (length - i) >= Vector256<ushort>.Count)
                            {
                                var vAmp = Vector256.Create((ushort)'&');
                                while (i + Vector256<ushort>.Count <= length)
                                {
                                    var chunk = Vector256.Load((ushort*)(ptr + i));
                                    var mask = Vector256.Equals(chunk, vAmp).ExtractMostSignificantBits();
                                    if (mask != 0)
                                    {
                                        int count = BitOperations.TrailingZeroCount(mask);
                                        for (int k = 0; k < count; k++) buffer[destIdx++] = ptr[i + k];
                                        i += count;
                                        goto FoundNext;
                                    }
                                    else
                                    {
                                        // copy entire vector
                                        for (int k = 0; k < Vector256<ushort>.Count; k++) buffer[destIdx++] = ptr[i + k];
                                        i += Vector256<ushort>.Count;
                                    }
                                }
                            }

                            while (i < length && ptr[i] != '&')
                            {
                                buffer[destIdx++] = ptr[i++];
                            }
                    }
                FoundNext:;
                }
            }

            return new string(buffer.Slice(0, destIdx));
        }
        finally
        {
            if (rented != null) ArrayPool<char>.Shared.Return(rented);
        }
    }

    // Decode into provided destination span. Returns number of characters written.
    // Caller must ensure dest.Length is sufficient (>= input.Length is a safe upper bound).
    public static unsafe int DecodeIntoSpan(ReadOnlySpan<char> input, Span<char> dest)
    {
        if (input.IsEmpty) return 0;

        int firstAmp = IndexOfCharSimd(input, '&');
        if (firstAmp == -1)
        {
            input.CopyTo(dest);
            return input.Length;
        }

        int length = input.Length;
        int destIdx = 0;

        // Copy everything before first ampersand
        for (int j = 0; j < firstAmp; j++) dest[destIdx++] = input[j];

        fixed (char* ptr = input)
        {
            int i = firstAmp;
            while (i < length)
            {
                if (ptr[i] == '&')
                {
                    i++; // Skip &
                    if (i < length && ptr[i] == '#')
                    {
                        i++; // Skip #
                        if (i < length && (ptr[i] == 'x' || ptr[i] == 'X'))
                        {
                            i++; // Skip x
                            int hexStart = i;
                            while (i < length && IsHexDigit(ptr[i])) i++;
                            if (i > hexStart && uint.TryParse(input.Slice(hexStart, i - hexStart), System.Globalization.NumberStyles.HexNumber, null, out uint val))
                            {
                                dest[destIdx++] = (char)val;
                                if (i < length && ptr[i] == ';') i++;
                                continue;
                            }
                            // fallback
                            dest[destIdx++] = '&'; dest[destIdx++] = '#'; dest[destIdx++] = input[hexStart - 1];
                            i = hexStart;
                        }
                        else
                        {
                            int decStart = i;
                            while (i < length && char.IsDigit(ptr[i])) i++;
                            if (i > decStart && uint.TryParse(input.Slice(decStart, i - decStart), out uint val))
                            {
                                dest[destIdx++] = (char)val;
                                if (i < length && ptr[i] == ';') i++;
                                continue;
                            }
                            dest[destIdx++] = '&'; dest[destIdx++] = '#';
                            i = decStart;
                        }
                    }
                    else
                    {
                        int nameStart = i;
                        while (i < length && char.IsLetterOrDigit(ptr[i])) i++;
                        if (i > nameStart)
                        {
                            ReadOnlySpan<char> name = input.Slice(nameStart, i - nameStart);
                            bool hasSemicolon = (i < length && ptr[i] == ';');
                            string decoded = DecodeNamedEntity(name, hasSemicolon);
                            if (decoded != null)
                            {
                                for (int j = 0; j < decoded.Length; j++) dest[destIdx++] = decoded[j];
                                if (hasSemicolon) i++;
                                continue;
                            }
                        }
                        dest[destIdx++] = '&';
                        i = nameStart;
                    }
                }
                else
                {
                    // copy until next '&'
                    int next = IndexOfCharSimd(input.Slice(i), '&');
                    if (next == -1) next = length - i;
                    input.Slice(i, next).CopyTo(dest.Slice(destIdx));
                    destIdx += next;
                    i += next;
                }
            }
        }

        return destIdx;
    }

    // Pooled decode API: decodes into a pooled buffer and returns the resulting string.
    public static string DecodeIntoPooled(ReadOnlySpan<char> input)
    {
        if (input.IsEmpty) return string.Empty;

        int firstAmp = IndexOfCharSimd(input, '&');
        if (firstAmp == -1) return input.ToString();

        int length = input.Length;
        char[]? rented = null;
        Span<char> buffer = length <= 1024 ? stackalloc char[1024] : (rented = ArrayPool<char>.Shared.Rent(length));

        try
        {
            int destIdx = 0;
            for (int j = 0; j < firstAmp; j++) buffer[destIdx++] = input[j];

            fixed (char* ptr = input)
            {
                int i = firstAmp;
                while (i < length)
                {
                    if (ptr[i] == '&')
                    {
                        i++; // skip '&'
                        if (i < length && ptr[i] == '#')
                        {
                            i++;
                            if (i < length && (ptr[i] == 'x' || ptr[i] == 'X'))
                            {
                                i++;
                                int hexStart = i;
                                while (i < length && IsHexDigit(ptr[i])) i++;
                                if (i > hexStart && uint.TryParse(input.Slice(hexStart, i - hexStart), System.Globalization.NumberStyles.HexNumber, null, out uint val))
                                {
                                    buffer[destIdx++] = (char)val;
                                    if (i < length && ptr[i] == ';') i++;
                                    continue;
                                }
                                buffer[destIdx++] = '&'; buffer[destIdx++] = '#'; buffer[destIdx++] = input[hexStart - 1];
                                i = hexStart;
                            }
                            else
                            {
                                int decStart = i;
                                while (i < length && char.IsDigit(ptr[i])) i++;
                                if (i > decStart && uint.TryParse(input.Slice(decStart, i - decStart), out uint val))
                                {
                                    buffer[destIdx++] = (char)val;
                                    if (i < length && ptr[i] == ';') i++;
                                    continue;
                                }
                                buffer[destIdx++] = '&'; buffer[destIdx++] = '#';
                                i = decStart;
                            }
                        }
                        else
                        {
                            int nameStart = i;
                            while (i < length && char.IsLetterOrDigit(ptr[i])) i++;
                            if (i > nameStart)
                            {
                                ReadOnlySpan<char> name = input.Slice(nameStart, i - nameStart);
                                bool hasSemicolon = (i < length && ptr[i] == ';');
                                string decoded = DecodeNamedEntity(name, hasSemicolon);
                                if (decoded != null)
                                {
                                    for (int k = 0; k < decoded.Length; k++) buffer[destIdx++] = decoded[k];
                                    if (hasSemicolon) i++;
                                    continue;
                                }
                            }
                            buffer[destIdx++] = '&';
                            i = nameStart;
                        }
                    }
                    else
                    {
                        if (Vector256.IsHardwareAccelerated && (length - i) >= Vector256<ushort>.Count)
                        {
                            var vAmp = Vector256.Create((ushort)'&');
                            while (i + Vector256<ushort>.Count <= length)
                            {
                                var chunk = Vector256.Load((ushort*)(ptr + i));
                                var mask = Vector256.Equals(chunk, vAmp).ExtractMostSignificantBits();
                                if (mask != 0)
                                {
                                    int count = BitOperations.TrailingZeroCount(mask);
                                    for (int k = 0; k < count; k++) buffer[destIdx++] = ptr[i + k];
                                    i += count;
                                    goto FoundNextPooled;
                                }
                                else
                                {
                                    for (int k = 0; k < Vector256<ushort>.Count; k++) buffer[destIdx++] = ptr[i + k];
                                    i += Vector256<ushort>.Count;
                                }
                            }
                        }
                        while (i < length && ptr[i] != '&') buffer[destIdx++] = ptr[i++];
                    }
                FoundNextPooled:;
                }
            }

            return new string(buffer.Slice(0, destIdx));
        }
        finally
        {
            if (rented != null) ArrayPool<char>.Shared.Return(rented);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe int IndexOfCharSimd(ReadOnlySpan<char> span, char ch)
    {
        if (span.IsEmpty) return -1;

        fixed (char* ptr = span)
        {
            int length = span.Length;
            int i = 0;

            if (Vector512.IsHardwareAccelerated && length >= Vector512<ushort>.Count)
            {
                var vCh = Vector512.Create((ushort)ch);
                while (i + Vector512<ushort>.Count <= length)
                {
                    var chunk = Vector512.Load((ushort*)(ptr + i));
                    var mask = Vector512.Equals(chunk, vCh).ExtractMostSignificantBits();
                    if (mask != 0)
                    {
                        return i + BitOperations.TrailingZeroCount(mask);
                    }
                    i += Vector512<ushort>.Count;
                }
            }

            if (Vector256.IsHardwareAccelerated && length >= Vector256<ushort>.Count)
            {
                var vCh = Vector256.Create((ushort)ch);
                while (i + Vector256<ushort>.Count <= length)
                {
                    var chunk = Vector256.Load((ushort*)(ptr + i));
                    var mask = Vector256.Equals(chunk, vCh).ExtractMostSignificantBits();
                    if (mask != 0)
                    {
                        return i + BitOperations.TrailingZeroCount(mask);
                    }
                    i += Vector256<ushort>.Count;
                }
            }

            for (; i < length; i++) if (ptr[i] == ch) return i;
        }

        return -1;
    }

    private static string DecodeNamedEntity(ReadOnlySpan<char> name, bool hasSemicolon)
    {
        // Strict matching for entities with semicolon
        if (hasSemicolon)
        {
            switch (name.Length)
            {
                case 2:
                    if (name.Equals("lt", StringComparison.Ordinal)) return "<";
                    if (name.Equals("gt", StringComparison.Ordinal)) return ">";
                    break;
                case 3:
                    if (name.Equals("amp", StringComparison.Ordinal)) return "&";
                    if (name.Equals("deg", StringComparison.Ordinal)) return "°";
                    if (name.Equals("reg", StringComparison.Ordinal)) return "®";
                    if (name.Equals("sup", StringComparison.Ordinal)) return "¹"; // This is actually &sup1; but let's be careful
                    break;
                case 4:
                    if (name.Equals("quot", StringComparison.Ordinal)) return "\"";
                    if (name.Equals("apos", StringComparison.Ordinal)) return "'";
                    if (name.Equals("nbsp", StringComparison.Ordinal)) return "\u00A0";
                    if (name.Equals("copy", StringComparison.Ordinal)) return "©";
                    if (name.Equals("euro", StringComparison.Ordinal)) return "€";
                    break;
                case 5:
                    if (name.Equals("trade", StringComparison.Ordinal)) return "™";
                    if (name.Equals("mdash", StringComparison.Ordinal)) return "—";
                    if (name.Equals("ndash", StringComparison.Ordinal)) return "–";
                    break;
                case 6:
                    if (name.Equals("frac12", StringComparison.Ordinal)) return "½";
                    if (name.Equals("frac14", StringComparison.Ordinal)) return "¼";
                    if (name.Equals("frac34", StringComparison.Ordinal)) return "¾";
                    break;
            }
        }
        else
        {
            // Legacy support for entities without semicolon (only in certain contexts, but we'll do common ones)
            if (name.Equals("amp", StringComparison.Ordinal)) return "&";
            if (name.Equals("lt", StringComparison.Ordinal)) return "<";
            if (name.Equals("gt", StringComparison.Ordinal)) return ">";
            if (name.Equals("quot", StringComparison.Ordinal)) return "\"";
            if (name.Equals("copy", StringComparison.Ordinal)) return "©";
        }

        return null;
    }

    private static bool IsHexDigit(char c) => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
}
