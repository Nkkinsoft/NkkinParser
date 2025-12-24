using System;

namespace NkkinParser;

internal static class HtmlUtils
{
    public static ReadOnlySpan<char> ExtractTagName(ReadOnlySpan<char> tag)
    {
        int i = 0;
        while (i < tag.Length && (tag[i] == '<' || tag[i] == '/')) i++;
        int start = i;
        while (i < tag.Length && !char.IsWhiteSpace(tag[i]) && tag[i] != '>' && tag[i] != '/') i++;
        return tag.Slice(start, i - start);
    }
}
