using Xunit;
using NkkinParser;

namespace NkkinParser.Tests;

public class RobustnessTests
{
    [Fact]
    public void AutoClose_P_Tags_Works()
    {
        var html = "<p>P1<div>Div</div><p>P2";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();
        
        // Expected structure:
        // html -> body -> p -> P1
        //             -> div -> Div
        //             -> p -> P2
        
        var body = doc.DocumentElement;
        Assert.Equal("html", body?.TagName);
        
        var children = body!.Children().ToList();
        Assert.Equal(3, children.Count);
        Assert.Equal("p", children[0].TagName);
        Assert.Equal("div", children[1].TagName);
        Assert.Equal("p", children[2].TagName);
    }

    [Fact]
    public void AutoClose_LI_Tags_Works()
    {
        var html = "<ul><li>Item 1<li>Item 2</ul>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();
        
        var ul = doc.DocumentElement?.FirstChild as Element;
        Assert.Equal("ul", ul?.TagName);
        
        var items = ul!.Children().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal("li", items[0].TagName);
        Assert.Equal("li", items[1].TagName);
        Assert.Equal("Item 1", items[0].TextContent.Trim());
    }

    [Fact]
    public void FosterParenting_Table_Works()
    {
        var html = "<table>Misplaced<tr><td>Cell</td></tr></table>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();
        
        // "Misplaced" should be moved before the table
        var body = doc.DocumentElement;
        var first = body?.FirstChild;
        Assert.True(first is Text, "First child should be the foster-parented text");
        Assert.Equal("Misplaced", first!.TextContent.Trim());
        
        var table = first.NextSibling as Element;
        Assert.Equal("table", table?.TagName);
    }

    [Fact]
    public void Merge_Body_Attributes_Works()
    {
        var html = "<body class='a'><body id='b' class='c'>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();
        
        var body = doc.DocumentElement; // For now our parser uses html as root and puts body inside if missing, or just uses the first one.
        // Wait, our parser currently allocate "html" if first tag is not html.
        // Let's check where the body is.
        var bodyElement = body?.TagName == "body" ? body : body?.FirstChild as Element;
        Assert.Equal("body", bodyElement?.TagName);
        Assert.Equal("b", bodyElement?.Attributes.Get("id"));
        // class should be 'a' (first one wins in spec for html/body merging)
        Assert.Equal("a", bodyElement?.Attributes.Get("class"));
    }

    [Fact]
    public void Malformed_Formatting_Works()
    {
        var html = "<b><i>text</b></i>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();
        
        // Adoption agency should handle this
        // Expected: <b><i>text</i></b><i></i> (simplified version)
        // Or at least not crash and produce <b><i>text</i></b>
        var b = doc.DocumentElement?.FirstChild as Element;
        Assert.Equal("b", b?.TagName);
        var i = b?.FirstChild as Element;
        Assert.Equal("i", i?.TagName);
        Assert.Equal("text", i?.TextContent);
    }
}

public static class NodeExtensions
{
    public static IEnumerable<Element> Children(this Node node)
    {
        for (var child = node.FirstChild; child != null; child = child.NextSibling)
        {
            if (child is Element el) yield return el;
        }
    }
}
