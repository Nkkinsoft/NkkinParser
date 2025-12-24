using System.Xml.Linq;
using Xunit;

namespace NkkinParser.Tests;

public class MalformedHtmlTests
{
    [Fact]
    public void UnclosedTags_AreTolerated()
    {
        var html = "<html><body><p>Hello<div>World";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();

        Assert.NotNull(doc.DocumentElement);
        var p = doc.DocumentElement.FirstChild?.FirstChild as Element;
        Assert.Equal("p", p?.TagName.ToString());
    }

    [Fact]
    public void BogusComments_AreIgnored()
    {
        var html = "<!---><p>Test</p>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();

        var p = doc.DocumentElement!.FirstChild as Element;
        Assert.Equal("p", p!.TagName.ToString());
    }

    [Fact]
    public void MissingClosingTags_RecoverGracefully()
    {
        var html = "<ul><li>One<li>Two<li>Three";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();

        var ul = doc.DocumentElement!.FirstChild as Element;
        Assert.Equal("ul", ul!.TagName.ToString());
        Assert.Equal(3, CountChildren(ul));
    }

    private int CountChildren(Element e)
    {
        int count = 0;
        for (var child = e.FirstChild; child != null; child = child.NextSibling)
            count++;
        return count;
    }
}