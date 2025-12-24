using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace NkkinParser;

/// <summary>
/// A high-performance, low-allocation string builder that uses stack-allocated initial buffers
/// and rents larger buffers from ArrayPool if needed.
/// </summary>
internal ref struct ValueStringBuilder
{
    private char[]? _arrayToReturnToPool;
    private Span<char> _chars;
    private int _pos;

    public ValueStringBuilder(Span<char> initialBuffer)
    {
        _arrayToReturnToPool = null;
        _chars = initialBuffer;
        _pos = 0;
    }

    public int Length
    {
        get => _pos;
        set
        {
            int delta = value - _pos;
            if (delta > 0)
            {
                Grow(delta);
            }
            _pos = value;
        }
    }

    public void Append(char c)
    {
        int pos = _pos;
        if ((uint)pos < (uint)_chars.Length)
        {
            _chars[pos] = c;
            _pos = pos + 1;
        }
        else
        {
            GrowAndAppend(c);
        }
    }

    public void Append(string? s)
    {
        if (s == null) return;
        Append(s.AsSpan());
    }

    public void Append(ReadOnlySpan<char> value)
    {
        int pos = _pos;
        if (pos > _chars.Length - value.Length)
        {
            Grow(value.Length);
        }

        value.CopyTo(_chars.Slice(_pos));
        _pos += value.Length;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void GrowAndAppend(char c)
    {
        Grow(1);
        Append(c);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int requiredAdditionalCapacity)
    {
        int newCapacity = Math.Max(_chars.Length + requiredAdditionalCapacity, _chars.Length * 2);
        char[] poolArray = ArrayPool<char>.Shared.Rent(newCapacity);

        _chars.CopyTo(poolArray);

        char[]? toReturn = _arrayToReturnToPool;
        _chars = _arrayToReturnToPool = poolArray;
        if (toReturn != null)
        {
            ArrayPool<char>.Shared.Return(toReturn);
        }
    }

    public override string ToString()
    {
        string s = new string(_chars.Slice(0, _pos));
        Dispose();
        return s;
    }

    public ReadOnlySpan<char> AsSpan() => _chars.Slice(0, _pos);

    public void Dispose()
    {
        char[]? toReturn = _arrayToReturnToPool;
        this = default; // Clear fields
        if (toReturn != null)
        {
            ArrayPool<char>.Shared.Return(toReturn);
        }
    }
}
