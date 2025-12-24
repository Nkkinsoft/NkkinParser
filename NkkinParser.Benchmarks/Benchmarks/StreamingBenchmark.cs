using BenchmarkDotNet.Attributes;
using System.Threading.Tasks;
using System.IO;
using System.Linq;

namespace NkkinParser.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class StreamingBenchmark
{
    private string _htmlContent = string.Empty;

    [GlobalSetup]
    public void Setup()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<html><body>");
        for (int i = 0; i < 10000; i++)
        {
            sb.Append($"<div class='node-{i}' id='div-{i}'><p>Paragraph {i}</p><a href='/link/{i}'>Link {i}</a></div>");
        }
        sb.Append("</body></html>");
        _htmlContent = sb.ToString();
    }

    [Benchmark]
    public async Task NkkinParser_ParseAsync()
    {
        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(_htmlContent));
        await foreach (var node in NkkinParser.HtmlParser.ParseAsync(ms))
        {
            // iterate to consume stream
        }
    }

    [Benchmark(Baseline = true)]
    public void NkkinParser_Parse()
    {
        using var parser = new NkkinParser.HtmlParser(_htmlContent);
        var doc = parser.Parse();
        // iterate nodes
        Traverse(doc.DocumentElement);
    }

    private void Traverse(NkkinParser.Node? node)
    {
        if (node == null) return;
        for (var child = node.FirstChild; child != null; child = child.NextSibling)
            Traverse(child);
    }
}
