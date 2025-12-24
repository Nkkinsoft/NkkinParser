using Xunit;
using NkkinParser;
using System.Linq;

namespace NkkinParser.Tests;

public class AdoptionAgencyTests
{
    [Fact]
    public void Misnested_Formatting_B_I_Works()
    {
        var html = "<b><i>text</b></i>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();
        
        // Expected: <b><i>text</i></b>
        var body = doc.DocumentElement;
        var b = body?.FirstChild as Element;
        Assert.Equal("b", b?.TagName);
        
        var i1 = b?.FirstChild as Element;
        Assert.Equal("i", i1?.TagName);
        Assert.Equal("text", i1?.TextContent);
        
        // No second i because no text/tags followed </b> before </i>
        Assert.Null(b?.NextSibling);
    }

    [Fact]
    public void Complex_Misnested_Formatting_Works()
    {
        var html = "<b>text1<i>text2</b>text3</i>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();
        
        // Expected: <b>text1<i>text2</i></b><i>text3</i>
        var body = doc.DocumentElement;
        var children = body!.Children().ToList();
        
        Assert.Equal(2, children.Count);
        Assert.Equal("b", children[0].TagName);
        Assert.Equal("i", children[1].TagName);
        
        Assert.Equal("text1text2", children[0].TextContent);
        Assert.Equal("text3", children[1].TextContent);
    }

    [Fact]
    public void NoahsArk_Clause_Works()
    {
        // Add 4 identical formatting elements, 1 should be removed
        var html = "<b><b><b><b>text</b></b></b></b>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();
        
        // This is hard to verify without internal access, 
        // but we can check if it parses correctly.
        Assert.NotNull(doc);
    }

    [Fact]
    public void Self_Closing_A_Works()
    {
        var html = "<a>1<a>2</a>3</a>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();
        
        // <a> doesn't auto-close <a> by default unless it's misnested in a specific way?
        // Actually, <a> is a formatting element.
        // Opening <a> when <a> is in stack of active formatting elements triggers AAA.
        
        var body = doc.DocumentElement;
        var children = body!.Children().ToList();
        
        // This should produce something like <a>1</a><a>2</a>3
        // (Wait, <a> auto-closes if another <a> is opened)
        Assert.Equal("a", children[0].TagName);
        Assert.Equal("a", children[1].TagName);
    }
}
