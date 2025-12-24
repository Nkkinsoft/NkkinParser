using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NkkinParser;

/// <summary>
/// A zero-allocation string interner optimized for HTML tag/attribute names.
/// Uses arena-backed storage to ensure interned strings live as long as the parser.
/// </summary>
public sealed class StringInterner
{
    private readonly ArenaAllocator _arena;
    private readonly Entry[] _entries;
    private readonly int _mask;

    private struct Entry
    {
        public string Value;
        public int Hash;
    }

    public StringInterner(ArenaAllocator arena, int capacity = 4096)
    {
        _arena = arena;
        int size = 1;
        while (size < capacity) size <<= 1;
        _entries = new Entry[size];
        _mask = size - 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string Intern(ReadOnlySpan<char> span)
    {
        if (span.IsEmpty) return string.Empty;

        int hash = GetHashCode(span);
        int index = hash & _mask;

        while (true)
        {
            ref var entry = ref _entries[index];
            if (entry.Value == null)
            {
                string s = new string(span);
                entry.Value = s;
                entry.Hash = hash;
                return s;
            }

            if (entry.Hash == hash && span.Equals(entry.Value, StringComparison.Ordinal))
            {
                return entry.Value;
            }

            index = (index + 1) & _mask;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetHashCode(ReadOnlySpan<char> span)
    {
        // FNV-1a
        uint hash = 2166136261;
        foreach (char c in span)
        {
            hash = (hash ^ c) * 16777619;
        }
        return (int)hash;
    }
}
