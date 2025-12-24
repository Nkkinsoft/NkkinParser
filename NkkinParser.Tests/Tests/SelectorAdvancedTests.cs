using Xunit;
using NkkinParser;
using NkkinParser.Selectors;

namespace NkkinParser.Tests;

public class SelectorAdvancedTests
{
    [Fact]
    public void MultiPartSelector_Descendant_Works()
    {
        var html = "<div><p><span>Match</span></p></div>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();

        var results = doc.QuerySelectorAll("div span");
        Assert.Single(results);
        Assert.Equal("span", results[0].TagName);
    }

    [Fact]
    public void MultiPartSelector_Child_Works()
    {
        var html = "<div><p><span>Match</span></p><span>No Match</span></div>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();

        var results = doc.QuerySelectorAll("div > span");
        Assert.Single(results);
        Assert.Equal("No Match", results[0].TextContent.Trim());
    }

    [Fact]
    public void SiblingSelector_Adjacent_Works()
    {
        var html = "<div><h1>Title</h1><p>First</p><p>Second</p></div>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();

        var results = doc.QuerySelectorAll("h1 + p");
        Assert.Single(results);
        Assert.Equal("First", results[0].TextContent.Trim());
    }

    [Fact]
    public void SiblingSelector_Subsequent_Works()
    {
        var html = "<div><h1>Title</h1><p>First</p><span>Other</span><p>Second</p></div>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();

        var results = doc.QuerySelectorAll("h1 ~ p");
        Assert.Equal(2, results.Count);
        Assert.Equal("First", results[0].TextContent.Trim());
        Assert.Equal("Second", results[1].TextContent.Trim());
    }

    [Fact]
    public void AttributeSelector_Exists_Works()
    {
        var html = "<div data-test></div><div data-other='val'></div><div></div>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();

        var results = doc.QuerySelectorAll("[data-test]");
        Assert.Single(results);
    }

    [Fact]
    public void AttributeSelector_Equals_Works()
    {
        var html = "<div class='btn primary'></div><div class='btn secondary'></div>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();

        var results = doc.QuerySelectorAll("[class='btn primary']");
        Assert.Single(results);
    }

    [Fact]
    public void AttributeSelector_StartsWith_Works()
    {
        var html = "<a href='https://google.com'></a><a href='http://bing.com'></a>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();

        var results = doc.QuerySelectorAll("a[href^='https']");
        Assert.Single(results);
    }

    [Fact]
    public void AttributeSelector_Contains_Works()
    {
        var html = "<div data-status='active-user'></div><div data-status='inactive-user'></div>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();

        var results = doc.QuerySelectorAll("[data-status*='active']");
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void ComplexSelector_Works()
    {
        var html = "<div id='main'><ul><li class='item' data-id='1'>One</li><li class='item' data-id='2'>Two</li></ul></div>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();

        var results = doc.QuerySelectorAll("#main ul li.item[data-id='2']");
        Assert.Single(results);
        Assert.Equal("Two", results[0].TextContent.Trim());
    }
}
