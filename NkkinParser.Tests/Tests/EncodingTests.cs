using Xunit;
using NkkinParser;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace NkkinParser.Tests;

public class EncodingTests
{
    static EncodingTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Fact]
    public void Detect_Utf8_WithBom_Works()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF, (byte)'<', (byte)'d', (byte)'i', (byte)'v', (byte)'>', (byte)'<', (byte)'/', (byte)'d', (byte)'i', (byte)'v', (byte)'>' };
        using var parser = new HtmlParser(bytes);
        Assert.Equal(Encoding.UTF8.WebName, parser.DetectedEncoding?.WebName);
        var doc = parser.Parse();
        // Standard parser wraps in <html>
        Assert.Equal("html", doc.DocumentElement?.TagName);
        Assert.Equal("div", (doc.DocumentElement?.FirstChild as Element)?.TagName);
    }

    [Fact]
    public void Detect_Utf16LE_WithBom_Works()
    {
        var content = "<div></div>";
        var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(content)).ToArray();
        using var parser = new HtmlParser(bytes);
        Assert.Equal(Encoding.Unicode.WebName, parser.DetectedEncoding?.WebName);
        var doc = parser.Parse();
        Assert.Equal("html", doc.DocumentElement?.TagName);
        Assert.Equal("div", (doc.DocumentElement?.FirstChild as Element)?.TagName);
    }

    [Fact]
    public void Detect_MetaCharset_Utf8_Works()
    {
        var html = "<html><head><meta charset=\"utf-8\"></head><body></body></html>";
        var bytes = Encoding.UTF8.GetBytes(html);
        using var parser = new HtmlParser(bytes);
        Assert.Equal(Encoding.UTF8.WebName, parser.DetectedEncoding?.WebName);
    }

    [Fact]
    public void Detect_MetaCharset_Windows1252_Works()
    {
        var html = "<html><head><meta charset=\"windows-1252\"></head><body>été</body></html>";
        var encoding1252 = Encoding.GetEncoding("Windows-1252");
        var bytes = encoding1252.GetBytes(html);
        
        using var parser = new HtmlParser(bytes);
        Assert.Equal(encoding1252.WebName, parser.DetectedEncoding?.WebName);
        
        var doc = parser.Parse();
        Assert.Contains("été", doc.DocumentElement?.TextContent);
    }

    [Fact]
    public void Detect_MetaHttpEquiv_Works()
    {
        var html = "<html><head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=iso-8859-1\"></head></html>";
        var bytes = Encoding.ASCII.GetBytes(html);
        using var parser = new HtmlParser(bytes);
        Assert.Equal(Encoding.GetEncoding("iso-8859-1").WebName, parser.DetectedEncoding?.WebName);
    }
}

public static class EnumerableExtensions
{
    public static IEnumerable<T> Concat<T>(this IEnumerable<T> first, IEnumerable<T> second)
    {
        foreach (var item in first) yield return item;
        foreach (var item in second) yield return item;
    }
}
