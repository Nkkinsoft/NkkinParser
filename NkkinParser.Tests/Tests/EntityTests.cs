using Xunit;
using NkkinParser;

namespace NkkinParser.Tests;

public class EntityTests
{
    [Fact]
    public void Decode_NamedEntities_Works()
    {
        Assert.Equal("<", HtmlEntityDecoder.Decode("&lt;"));
        Assert.Equal(">", HtmlEntityDecoder.Decode("&gt;"));
        Assert.Equal("&", HtmlEntityDecoder.Decode("&amp;"));
        Assert.Equal("\"", HtmlEntityDecoder.Decode("&quot;"));
        Assert.Equal("'", HtmlEntityDecoder.Decode("&apos;"));
        Assert.Equal("\u00A0", HtmlEntityDecoder.Decode("&nbsp;"));
        Assert.Equal("©", HtmlEntityDecoder.Decode("&copy;"));
        Assert.Equal("€", HtmlEntityDecoder.Decode("&euro;"));
        Assert.Equal("™", HtmlEntityDecoder.Decode("&trade;"));
    }

    [Fact]
    public void Decode_NumericEntities_Works()
    {
        // Decimal
        Assert.Equal("\n", HtmlEntityDecoder.Decode("&#10;"));
        Assert.Equal("A", HtmlEntityDecoder.Decode("&#65;"));
        
        // Hex
        Assert.Equal("\n", HtmlEntityDecoder.Decode("&#x0A;"));
        Assert.Equal("A", HtmlEntityDecoder.Decode("&#x41;"));
        Assert.Equal("€", HtmlEntityDecoder.Decode("&#x20AC;"));
    }

    [Fact]
    public void Decode_LegacyEntities_Works()
    {
        // Without semicolon
        Assert.Equal("&", HtmlEntityDecoder.Decode("&amp"));
        Assert.Equal("<", HtmlEntityDecoder.Decode("&lt"));
        Assert.Equal(">", HtmlEntityDecoder.Decode("&gt"));
        Assert.Equal("©", HtmlEntityDecoder.Decode("&copy"));
        
        // Mixed text
        Assert.Equal("Rock & Roll", HtmlEntityDecoder.Decode("Rock &amp Roll"));
    }

    [Fact]
    public void Decode_ComplexText_Works()
    {
        var input = "The &ldquo;quick&rdquo; brown fox &amp; the &frac12; moon.";
        // Note: &ldquo; and &rdquo; are not in our common subset yet, so they should stay raw
        var expected = "The &ldquo;quick&rdquo; brown fox & the ½ moon.";
        Assert.Equal(expected, HtmlEntityDecoder.Decode(input));
    }

    [Fact]
    public void Decode_AttributeEntities_Works()
    {
        var html = "<div title='&quot;hello&quot; &amp; &copy;'></div>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();
        var div = doc.DocumentElement?.FirstChild as Element;
        Assert.Equal("\"hello\" & ©", div?.Attributes.Get("title"));
    }
}
