using Xunit;
using NkkinParser;
using NkkinParser.Selectors;

namespace NkkinParser.Tests;

public class SelectorPseudoTests
{
    [Fact]
    public void Pseudo_FirstChild_Works()
    {
        var html = "<ul><li>1</li><li>2</li></ul>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();
        
        var results = doc.QuerySelectorAll("li:first-child");
        Assert.Single(results);
        Assert.Equal("1", results[0].TextContent.Trim());
    }

    [Fact]
    public void Pseudo_LastChild_Works()
    {
        var html = "<ul><li>1</li><li>2</li></ul>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();
        
        var results = doc.QuerySelectorAll("li:last-child");
        Assert.Single(results);
        Assert.Equal("2", results[0].TextContent.Trim());
    }

    [Fact]
    public void Pseudo_NthChild_Works()
    {
        var html = "<ul><li>1</li><li>2</li><li>3</li></ul>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();
        
        var results = doc.QuerySelectorAll("li:nth-child(2)");
        Assert.Single(results);
        Assert.Equal("2", results[0].TextContent.Trim());
    }

    [Fact]
    public void Pseudo_Not_Works()
    {
        var html = "<div><p class='skip'>Skip</p><p>Match</p></div>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();
        
        var results = doc.QuerySelectorAll("p:not(.skip)");
        Assert.Single(results);
        Assert.Equal("Match", results[0].TextContent.Trim());
    }

    [Fact]
    public void Pseudo_Combined_Works()
    {
        var html = "<ul><li class='x'>1</li><li>2</li><li class='x'>3</li></ul>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();
        
        var results = doc.QuerySelectorAll("li.x:not(:first-child)");
        Assert.Single(results);
        Assert.Equal("3", results[0].TextContent.Trim());
    }
}
