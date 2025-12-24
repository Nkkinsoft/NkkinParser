using AngleSharp;
using AngleSharp.Html.Parser;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Collections.Generic;

namespace NkkinParser.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
public class ParsingBenchmark
{
    private string[] _htmlStrings = null!;

    [GlobalSetup]
    public void Setup()
    {
        _htmlStrings = new[]
        {
            GenerateSampleHtml(100),   // Small ~10KB
            GenerateSampleHtml(1000),  // Medium ~100KB
            GenerateSampleHtml(10000)  // Large ~1MB
        };
    }

    private static string GenerateSampleHtml(int elements)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<html><body>");
        for (int i = 0; i < elements; i++)
        {
            sb.Append($"<div class='node-{i}' id='div-{i}'>");
            sb.Append($"<p>This is paragraph {i} in the benchmark sample document.</p>");
            sb.Append($"<a href='/link/{i}'>Link {i}</a>");
            sb.Append("<ul><li>Item 1</li><li>Item 2</li></ul>");
            sb.Append("</div>");
        }
        sb.Append("</body></html>");
        return sb.ToString();
    }

    [Benchmark(Baseline = true)]
    public void AngleSharp_Parse()
    {
        var parser = new AngleSharp.Html.Parser.HtmlParser();

        foreach (var html in _htmlStrings)
        {
            var document = parser.ParseDocument(html);
            _ = document.DocumentElement.OuterHtml; // Force full parse/serialization
        }
    }

    [Benchmark]
    public void NkkinParser_Parse()
    {
        foreach (var html in _htmlStrings)
        {
            using var parser = new NkkinParser.HtmlParser(html);
            var document = parser.Parse();
            Traverse(document.DocumentElement);
        }
    }

    private void Traverse(NkkinParser.Node? node)
    {
        if (node == null) return;
        for (var child = node.FirstChild; child != null; child = child.NextSibling)
        {
            Traverse(child);
        }
    }
}
