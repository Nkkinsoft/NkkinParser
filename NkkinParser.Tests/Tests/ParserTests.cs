using System.Xml.Linq;
using Xunit;
using static System.Net.Mime.MediaTypeNames;

namespace NkkinParser.Tests;

public class ParserTests
{
    [Fact]
    public void SimpleDocument_BuildsTreeCorrectly()
    {
        var html = "<html><body><h1>Title</h1><p>Paragraph</p></body></html>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();

        Assert.NotNull(doc.DocumentElement);
        Assert.Equal("html", doc.DocumentElement.TagName.ToString());

        var body = doc.DocumentElement.FirstChild as Element;
        Assert.Equal("body", body!.TagName.ToString());

        var h1 = body.FirstChild as Element;
        Assert.Equal("h1", h1!.TagName.ToString());
        var titleText = h1.FirstChild as Text;
        Assert.Equal("Title", titleText!.Data.ToString());

        var p = h1.NextSibling as Element;
        Assert.Equal("p", p!.TagName.ToString());
    }

    [Fact]
    public void SelfClosingTags_AreHandled()
    {
        var html = "<img src='test.jpg' /><br/>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();

        var img = doc.DocumentElement!.FirstChild as Element;
        Assert.Equal("img", img!.TagName.ToString());

        var br = img.NextSibling as Element;
        Assert.Equal("br", br!.TagName.ToString());
    }
}