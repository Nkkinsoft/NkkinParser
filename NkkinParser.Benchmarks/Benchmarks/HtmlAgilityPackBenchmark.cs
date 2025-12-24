using BenchmarkDotNet.Attributes;
using HtmlAgilityPack;

namespace NkkinParser.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class HtmlAgilityPackBenchmark
{
    private string _html = string.Empty;

    [GlobalSetup]
    public void Setup()
    {
        // reuse selector benchmark sample
        var sb = new System.Text.StringBuilder();
        sb.Append("<html><body><div id='main'>");
        for (int i = 0; i < 5000; i++)
        {
            string className = (i % 10 == 0) ? "class-name" : "other-class";
            string href = (i % 5 == 0) ? $"href='/link/{i}'" : "";
            sb.Append($"<div class='{className}' id='div-{i}'>");
            sb.Append($"<p>Paragraph {i}</p>");
            if (!string.IsNullOrEmpty(href)) sb.Append($"<a {href}>Link {i}</a>");
            sb.Append("</div>");
        }
        sb.Append("<div id='search'>Search box</div>");
        sb.Append("</div></body></html>");
        _html = sb.ToString();
    }

    [Benchmark]
    public void HtmlAgilityPack_Select()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(_html);
        var nodes = doc.DocumentNode.SelectNodes("//a[@href]|//div|//span[@class='class-name']|//*[@id='search']");
        if (nodes != null)
        {
            foreach (var _ in nodes) { }
        }
    }
}
