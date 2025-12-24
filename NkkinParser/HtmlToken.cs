namespace NkkinParser;

public enum HtmlTokenKind : byte
{
    Eof,
    Text,
    TagStart,
    TagEnd,
    Comment,
    Doctype
}

public readonly ref struct HtmlToken
{
    public HtmlTokenKind Kind { get; }
    public ReadOnlySpan<char> Value { get; }

    public HtmlToken(HtmlTokenKind kind, ReadOnlySpan<char> value)
    {
        Kind = kind;
        Value = value;
    }
}
