using Xunit;

namespace NkkinParser.Tests;

public class TokenizerTests
{
    [Fact]
    public void BasicHtml_TokenizesCorrectly()
    {
        var html = "<html><head><title>Test</title></head><body><p>Hello</p></body></html>";
        var tokenizer = new HtmlTokenizer(html);

        var tokens = new List<(HtmlTokenKind Kind, string Value)>();
        while (true)
        {
            var token = tokenizer.NextToken();
            if (token.Kind == HtmlTokenKind.Eof) break;
            tokens.Add((token.Kind, token.Value.ToString()));
        }

        Assert.Equal(12, tokens.Count);
        Assert.Equal(HtmlTokenKind.TagStart, tokens[0].Kind);
        Assert.Contains("html", tokens[0].Value);
        Assert.Equal(HtmlTokenKind.Text, tokens[8].Kind);
        Assert.Equal("Hello", tokens[8].Value.Trim());
    }

    [Fact]
    public void Comment_IsDetected()
    {
        var html = "<!-- this is a comment -->";
        var tokenizer = new HtmlTokenizer(html);
        var token = tokenizer.NextToken();

        Assert.Equal(HtmlTokenKind.Comment, token.Kind);
        Assert.Contains("this is a comment", token.Value.ToString());
    }
}