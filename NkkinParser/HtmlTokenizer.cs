using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace NkkinParser;

public ref struct HtmlTokenizer
{
    private readonly ReadOnlySpan<char> _source;
    private int _position;

    public HtmlTokenizer(ReadOnlySpan<char> source)
    {
        _source = source;
        _position = 0;
    }

    public int Position => _position;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsEof() => _position >= _source.Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Advance(int count = 1) => _position += count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private char Peek() => _position < _source.Length ? _source[_position] : '\0';

    public HtmlToken NextToken()
    {
        if (IsEof()) return new HtmlToken(HtmlTokenKind.Eof, default);

        int start = _position;
        char current = _source[_position];

        if (current == '<')
        {
            Advance();
            if (IsEof()) return new HtmlToken(HtmlTokenKind.Text, _source.Slice(start));

            char next = _source[_position];
            if (next == '!')
            {
                Advance();
                if (_position + 1 < _source.Length && _source[_position] == '-' && _source[_position+1] == '-')
                {
                    return ConsumeComment(start);
                }
                
                // If it starts with !DOCTYPE (case-insensitive), it's a doctype
                if (_source.Slice(_position).StartsWith("DOCTYPE".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    return ConsumeDoctype(start);
                }

                return ConsumeBogusComment(start);
            }

            if (next == '?')
            {
                Advance();
                return ConsumeBogusComment(start);
            }
            
            if (next == '/')
            {
                Advance();
                return ConsumeTag(start, HtmlTokenKind.TagEnd);
            }

            return ConsumeTag(start, HtmlTokenKind.TagStart);
        }

        SkipTextSimd();
        return new HtmlToken(HtmlTokenKind.Text, _source.Slice(start, _position - start));
    }

    private HtmlToken ConsumeTag(int start, HtmlTokenKind kind)
    {
        // Scan until '>', whitespace, or '/'
        while (!IsEof())
        {
            char c = _source[_position];
            if (c == '>' || char.IsWhiteSpace(c) || c == '/') break;
            Advance();
        }

        // Just return the raw tag block for now. The parser will use GetAttributes(HtmlToken)
        // which will also be SIMD-accelerated.
        while (!IsEof() && _source[_position] != '>') 
        { 
            if (_source[_position] == '"' || _source[_position] == '\'') 
            { 
                char quote = _source[_position]; 
                Advance(); 
                int qIdx = _source.Slice(_position).IndexOf(quote); 
                if (qIdx >= 0) _position += qIdx + 1; 
                else _position = _source.Length; 
            } 
            else 
            { 
                Advance(); 
            } 
        } 
        if (!IsEof()) Advance(); // Skip '>' 
        return new HtmlToken(kind, _source.Slice(start, _position - start));
    }

    public unsafe void GetAttributes(HtmlToken token, AttributeCollection attributes, StringInterner interner)
    {
        ReadOnlySpan<char> span = token.Value;
        if (span.Length < 3) return;

        // Ensure collection is empty before populating
        attributes.Clear();

        // Skip '<' and tag name
        int i = 1;
        while (i < span.Length && !char.IsWhiteSpace(span[i]) && span[i] != '>' && span[i] != '/') i++;

        fixed (char* ptr = span)
        {
            while (i < span.Length)
            {
                // Skip whitespace
                while (i < span.Length && char.IsWhiteSpace(ptr[i])) i++;
                if (i >= span.Length || ptr[i] == '>' || ptr[i] == '/') break;

                // Start of attribute name
                int nameStart = i;
                
                // SIMD scan for '=' or whitespace or '>'
                if (Vector256.IsHardwareAccelerated && (span.Length - i) >= Vector256<ushort>.Count)
                {
                    var vEq = Vector256.Create((ushort)'=');
                    var vSpace = Vector256.Create((ushort)' ');
                    var vTab = Vector256.Create((ushort)'\t');
                    var vCr = Vector256.Create((ushort)'\r');
                    var vLf = Vector256.Create((ushort)'\n');
                    var vGt = Vector256.Create((ushort)'>');
                    
                    while (i + Vector256<ushort>.Count <= span.Length)
                    {
                        var chunk = Vector256.Load((ushort*)(ptr + i));
                        var mask = (Vector256.Equals(chunk, vEq) | 
                                    Vector256.Equals(chunk, vSpace) | 
                                    Vector256.Equals(chunk, vTab) | 
                                    Vector256.Equals(chunk, vCr) | 
                                    Vector256.Equals(chunk, vLf) | 
                                    Vector256.Equals(chunk, vGt)).ExtractMostSignificantBits();
                        if (mask != 0)
                        {
                            i += BitOperations.TrailingZeroCount(mask);
                            goto FoundNameEnd;
                        }
                        i += Vector256<ushort>.Count;
                    }
                }

                while (i < span.Length && ptr[i] != '=' && !char.IsWhiteSpace(ptr[i]) && ptr[i] != '>') i++;

            FoundNameEnd:
                int nameEnd = i;
                ReadOnlySpan<char> name = span.Slice(nameStart, nameEnd - nameStart);

                // Skip whitespace
                while (i < span.Length && char.IsWhiteSpace(ptr[i])) i++;

                ReadOnlySpan<char> value = default;
                if (i < span.Length && ptr[i] == '=')
                {
                    i++; // Skip '='
                    while (i < span.Length && char.IsWhiteSpace(ptr[i])) i++;

                    if (i < span.Length)
                    {
                        if (ptr[i] == '\"' || ptr[i] == '\'')
                        {
                            char quote = ptr[i];
                            i++;
                            int valStart = i;
                            int qIdx = span.Slice(i).IndexOf(quote);
                            if (qIdx >= 0)
                            {
                                value = span.Slice(valStart, qIdx);
                                i += qIdx + 1;
                            }
                            else
                            {
                                value = span.Slice(valStart);
                                i = span.Length;
                            }
                        }
                        else
                        {
                            int valStart = i;
                            // SIMD scan for whitespace or '>'
                            if (Vector256.IsHardwareAccelerated && (span.Length - i) >= Vector256<ushort>.Count)
                            {
                                var vSpace = Vector256.Create((ushort)' ');
                                var vTab = Vector256.Create((ushort)'\t');
                                var vCr = Vector256.Create((ushort)'\r');
                                var vLf = Vector256.Create((ushort)'\n');
                                var vGt = Vector256.Create((ushort)'>');
                                
                                while (i + Vector256<ushort>.Count <= span.Length)
                                {
                                    var chunk = Vector256.Load((ushort*)(ptr + i));
                                    var mask = (Vector256.Equals(chunk, vSpace) | 
                                                Vector256.Equals(chunk, vTab) | 
                                                Vector256.Equals(chunk, vCr) | 
                                                Vector256.Equals(chunk, vLf) | 
                                                Vector256.Equals(chunk, vGt)).ExtractMostSignificantBits();
                                    if (mask != 0)
                                    {
                                        i += BitOperations.TrailingZeroCount(mask);
                                        goto FoundValEnd;
                                    }
                                    i += Vector256<ushort>.Count;
                                }
                            }

                            while (i < span.Length && !char.IsWhiteSpace(ptr[i]) && ptr[i] != '>') i++;
                        FoundValEnd:
                            value = span.Slice(valStart, i - valStart);
                        }
                    }

                }

                // Determine final value string. Avoid decoding/extra work if there are no entities ('&').
                string finalValue;
                if (value.Length == 0)
                {
                    finalValue = string.Empty;
                }
                else
                {
                    bool hasEntity = ContainsCharSimd(value, '&');

                    if (!hasEntity)
                    {
                        finalValue = value.ToString();
                    }
                    else
                    {
                        // Use pooled decoding to avoid intermediate temporary strings
                        finalValue = HtmlEntityDecoder.DecodeIntoPooled(value);
                    }
                }

                attributes.Add(interner.Intern(name), finalValue); // interning attribute names is critical
            }
        }
    }

    

    private HtmlToken ConsumeComment(int start)
    {
        // _position is currently at the first '-' of "<!--"
        int idx = _source.Slice(_position).IndexOf("-->".AsSpan(), StringComparison.Ordinal);
        if (idx >= 0)
        {
            _position = _position + idx + 3;
        }
        else
        {
            _position = _source.Length;
        }
        return new HtmlToken(HtmlTokenKind.Comment, _source.Slice(start, _position - start));
    }

    private HtmlToken ConsumeDoctype(int start)
    {
        int idx = _source.Slice(_position).IndexOf('>');
        if (idx >= 0) _position += idx + 1;
        else _position = _source.Length;
        return new HtmlToken(HtmlTokenKind.Doctype, _source.Slice(start, _position - start));
    }

    private HtmlToken ConsumeBogusComment(int start)
    {
        int idx = _source.Slice(_position).IndexOf('>');
        if (idx >= 0) _position += idx + 1;
        else _position = _source.Length;
        return new HtmlToken(HtmlTokenKind.Comment, _source.Slice(start, _position - start));
    }

    private unsafe void SkipTextSimd()
    {
        fixed (char* ptr = _source)
        {
            char* cur = ptr + _position;
            char* end = ptr + _source.Length;

            if (Vector512.IsHardwareAccelerated && (end - cur) >= Vector512<ushort>.Count)
            {
                var vLt = Vector512.Create((ushort)'<');
                var vAmp = Vector512.Create((ushort)'&');
                while (cur + Vector512<ushort>.Count <= end)
                {
                    var chunk = Vector512.Load((ushort*)cur);
                    var mask = (Vector512.Equals(chunk, vLt) | Vector512.Equals(chunk, vAmp)).ExtractMostSignificantBits();
                    if (mask != 0)
                    {
                        cur += BitOperations.TrailingZeroCount(mask);
                        goto Done;
                    }
                    cur += Vector512<ushort>.Count;
                }
            }
            else if (Vector256.IsHardwareAccelerated && (end - cur) >= Vector256<ushort>.Count)
            {
                var vLt = Vector256.Create((ushort)'<');
                var vAmp = Vector256.Create((ushort)'&');
                while (cur + Vector256<ushort>.Count <= end)
                {
                    var chunk = Vector256.Load((ushort*)cur);
                    var mask = (Vector256.Equals(chunk, vLt) | Vector256.Equals(chunk, vAmp)).ExtractMostSignificantBits();
                    if (mask != 0)
                    {
                        cur += BitOperations.TrailingZeroCount(mask);
                        goto Done;
                    }
                    cur += Vector256<ushort>.Count;
                }
            }

            while (cur < end && *cur != '<' && *cur != '&') cur++;

        Done:
            _position = (int)(cur - ptr);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe bool ContainsCharSimd(ReadOnlySpan<char> span, char ch)
    {
        if (span.IsEmpty) return false;

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
                    if (mask != 0) return true;
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
                    if (mask != 0) return true;
                    i += Vector256<ushort>.Count;
                }
            }

            for (; i < length; i++)
            {
                if (ptr[i] == ch) return true;
            }
        }

        return false;
    }
}
