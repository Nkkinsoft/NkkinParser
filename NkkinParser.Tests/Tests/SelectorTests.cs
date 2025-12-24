using Xunit;

namespace NkkinParser.Tests;

public class SelectorTests
{
    [Fact]
    public void Selectors_ShouldNotBeBlockedByTextNodes()
    {
        var html = "<div>Text content <p>Found me</p></div>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();

        // The 'p' is a sibling of a Text node. The current bug will miss it.
        var paras = doc.QuerySelectorAll("p");
        Assert.Single(paras);
        Assert.Equal("p", paras[0].TagName);
    }

    [Fact]
    public void Indexing_ShouldNotBeBlockedByTextNodes()
    {
        var html = "<div>Text content <p class='target'>Found me</p></div>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();
        
        var index = new Indexing.DocumentIndex(doc, new ArenaAllocator());
        var elements = index.GetByClass("target");
        
        Assert.NotNull(elements);
        Assert.Single(elements);
    }

    [Fact]
    public void ClassMatching_ShouldBeExact()
    {
        var html = "<div class='interactive-element'></div><div class='active'></div>";
        using var parser = new HtmlParser(html);
        var doc = parser.Parse();

        var active = doc.QuerySelectorAll(".active");
        Assert.Single(active); // Should NOT match 'interactive-element'
    }
}