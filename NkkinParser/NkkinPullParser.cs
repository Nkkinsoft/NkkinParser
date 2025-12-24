namespace NkkinParser;

public ref struct NkkinPullParser
{
    private HtmlTokenizer _tokenizer;

    public NodeType CurrentType { get; private set; }
    public ReadOnlySpan<char> CurrentName { get; private set; }
    public ReadOnlySpan<char> CurrentValue { get; private set; }

    public NkkinPullParser(ReadOnlySpan<char> input)
    {
        _tokenizer = new HtmlTokenizer(input);
        CurrentType = NodeType.None;
        CurrentName = default;
        CurrentValue = default;
    }

    public bool Read()
    {
        var token = _tokenizer.NextToken();
        if (token.Kind == HtmlTokenKind.Eof)
        {
            CurrentType = NodeType.None;
            return false;
        }

        CurrentType = token.Kind switch
        {
            HtmlTokenKind.Text => NodeType.Text,
            HtmlTokenKind.TagStart => NodeType.Element,
            HtmlTokenKind.TagEnd => NodeType.EndElement,
            HtmlTokenKind.Comment => NodeType.Comment,
            _ => NodeType.None
        };

        CurrentValue = token.Value;
        CurrentName = HtmlUtils.ExtractTagName(token.Value);
        return true;
    }

}

// NodeType moved to Node.cs
