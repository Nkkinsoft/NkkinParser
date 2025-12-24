using AngleSharp.Html.Parser;
using AngleSharp.Dom;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using System.IO;
using System.Linq;
using System.Net.Http;

namespace NkkinParser.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class SelectorBenchmark
{
    private NkkinParser.Document _nkkinDoc = null!;
    private NkkinParser.HtmlParser _nkkinParser = null!;
    private AngleSharp.Dom.IDocument _angleSharpDoc = null!;
    private NkkinParser.Indexing.DocumentIndex? _index;
    private NkkinParser.ArenaAllocator? _arena;

    [GlobalSetup]
    public void Setup()
    {
        var htmlContent = GenerateSampleHtml(5000); // 5000 elements for selector testing

        // NkkinParser
        _nkkinParser = new NkkinParser.HtmlParser(htmlContent);
        _nkkinDoc = _nkkinParser.Parse();
        // Build a document index to accelerate selector candidate selection
        _arena = new NkkinParser.ArenaAllocator();
        _index = new NkkinParser.Indexing.DocumentIndex(_nkkinDoc, _arena);

        // AngleSharp
        var angleSharpParser = new AngleSharp.Html.Parser.HtmlParser();
        _angleSharpDoc = angleSharpParser.ParseDocument(htmlContent);
    }

    private static string GenerateSampleHtml(int elements)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<html><body><div id='main'>");
        for (int i = 0; i < elements; i++)
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
        return sb.ToString();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _nkkinParser.Dispose();
        _arena?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public void AngleSharp_QuerySelectorAll()
    {
        var results = _angleSharpDoc.QuerySelectorAll("a[href], div, span.class-name, #search");
        foreach (var _ in results) { }
    }

    [Benchmark]
    public void NkkinParser_QuerySelectorAll()
    {
        var results = _nkkinDoc.QuerySelectorAll("a[href], div, span.class-name, #search", _index);
        foreach (var _ in results) { }
    }
}