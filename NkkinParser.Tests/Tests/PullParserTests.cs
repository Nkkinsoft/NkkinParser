using Xunit;

namespace NkkinParser.Tests;

public class PullParserTests
{
    [Fact]
    public void PullParser_ReadsNodesSequentially()
    {
        var html = "<div><span>Hello</span><p>World</p></div>";
        var pull = new NkkinPullParser(html);

        Assert.True(pull.Read());
        Assert.Equal(NodeType.Element, pull.CurrentType);
        Assert.Equal("div", pull.CurrentName.ToString());

        Assert.True(pull.Read());
        Assert.Equal(NodeType.Element, pull.CurrentType);
        Assert.Equal("span", pull.CurrentName.ToString());

        Assert.True(pull.Read());
        Assert.Equal(NodeType.Text, pull.CurrentType);
        Assert.Equal("Hello", pull.CurrentValue.ToString().Trim());

        Assert.True(pull.Read());
        Assert.Equal(NodeType.EndElement, pull.CurrentType);
        Assert.Equal("span", pull.CurrentName.ToString());

        Assert.True(pull.Read());
        Assert.Equal(NodeType.Element, pull.CurrentType);
        Assert.Equal("p", pull.CurrentName.ToString());
    }
}