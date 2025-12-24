using BenchmarkDotNet.Attributes;
using System;
using System.Buffers;
using System.Text;

namespace NkkinParser.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class AttributeParsingBenchmark
{
    private string _sampleTag = string.Empty;
    private NkkinParser.ArenaAllocator? _arena;
    private NkkinParser.StringInterner? _interner;

    [GlobalSetup]
    public void Setup()
    {
        // Create a tag with many attributes and some entities
        var sb = new StringBuilder();
        sb.Append("<div");
        for (int i = 0; i < 100; i++)
        {
            sb.Append(" ");
            sb.Append("data-attr").Append(i).Append("=\"");
            sb.Append("value").Append(i);
            if (i % 10 == 0) sb.Append(" &amp; &lt; &gt;");
            sb.Append("\"");
        }
        sb.Append("></div>");
        _sampleTag = sb.ToString();
    }

    [Benchmark]
    public void ParseAttributes()
    {
        var span = _sampleTag.AsSpan();
        var token = new NkkinParser.HtmlToken(NkkinParser.HtmlTokenKind.TagStart, span);
        var attrs = new NkkinParser.AttributeCollection();
        var arena = new NkkinParser.ArenaAllocator();
        var interner = new NkkinParser.StringInterner(arena);
        var tokenizer = new NkkinParser.HtmlTokenizer(_sampleTag);
        tokenizer.GetAttributes(token, attrs, interner);
    }
}
