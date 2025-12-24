using Xunit;
using NkkinParser;

namespace NkkinParser.Tests;

public class SerializationTests
{
    [Fact]
    public void Serialize_Basic_Works()
    {
        var html = "<div id=\"main\" class=\"container\"><p>Hello World</p><!-- comment --></div>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();
        
        var serialized = HtmlSerializer.Serialize(doc);
        Assert.Contains("<div id=\"main\" class=\"container\">", serialized);
        Assert.Contains("<p>Hello World</p>", serialized);
        Assert.Contains("<!-- comment -->", serialized);
        Assert.Contains("</div>", serialized);
    }

    [Fact]
    public void Serialize_AttributeEncoding_Works()
    {
        var html = "<div title=\"&quot;quoted&quot; &amp; more\"></div>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();
        
        var serialized = HtmlSerializer.Serialize(doc);
        // The parser decodes it, then serializer re-encodes it.
        // Input: "quoted" & more
        // Re-encoded: &quot;quoted&quot; &amp; more
        Assert.Contains("title=\"&quot;quoted&quot; &amp; more\"", serialized);
    }

    [Fact]
    public void Serialize_DeepTree_NoStackOverflow()
    {
        // Create a very deep tree
        var htmlBuilder = new System.Text.StringBuilder();
        for (int i = 0; i < 1000; i++) htmlBuilder.Append("<div>");
        for (int i = 0; i < 1000; i++) htmlBuilder.Append("</div>");
        
        using var parser = new HtmlParser(htmlBuilder.ToString());
        var doc = parser.Parse();
        
        // This would traditionally cause StackOverflow if recursive
        var serialized = HtmlSerializer.Serialize(doc);
        Assert.Equal(11013, serialized.Length);
    }

    [Fact]
    public void Serialize_VoidElements_NoEndTag()
    {
        var html = "<div><img src='test.png'><br><hr></div>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();
        
        var serialized = HtmlSerializer.Serialize(doc);
        Assert.Contains("<img src=\"test.png\">", serialized);
        Assert.DoesNotContain("</img>", serialized);
        Assert.Contains("<br>", serialized);
        Assert.DoesNotContain("</br>", serialized);
        Assert.Contains("<hr>", serialized);
        Assert.DoesNotContain("</hr>", serialized);
    }

    [Fact]
    public void Serialize_RoundTrip_Consistency()
    {
        var html = "<div class=\"a\"><b>Test</b></div>";
        using var parser1 = new HtmlParser(html);
        var doc1 = parser1.Parse();
        var s1 = HtmlSerializer.Serialize(doc1);

        using var parser2 = new HtmlParser(s1);
        var doc2 = parser2.Parse();
        var s2 = HtmlSerializer.Serialize(doc2);

        Assert.Equal(s1, s2);
    }
}
