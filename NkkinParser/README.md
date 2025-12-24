# NkkinParser 🚀

**The fastest, near-zero-allocation HTML5 parser for .NET 10**

[![.NET 10](https://img.shields.io/badge/.NET-10-blue.svg)](https://dotnet.microsoft.com/) [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT) [![Stars](https://img.shields.io/github/stars/Nkkinsoft/NkkinParser?style=social)](https://github.com/Nkkinsoft/NkkinParser)

NkkinParser is an ultra-high-performance, tolerant HTML5 parser and compiled CSS selector engine for .NET 10. Implemented with `ReadOnlySpan<char>`, unsafe hot paths, SIMD acceleration (AVX/Vector512), and arena-backed allocations, NkkinParser delivers substantially better throughput and far fewer heap allocations than common .NET parsers.

Key differentiators:

- SIMD-accelerated scanning and decoding where available
- Arena allocation + pooled buffers for near-zero allocations
- Compiled selectors with optional `DocumentIndex` (id/class/tag) for O(1) candidate selection
- Streaming pull parsing via `IAsyncEnumerable<Node>` for very large documents
- No external dependencies; pure .NET 10

---

## Performance summary (combined)

The repository includes a BenchmarkDotNet suite comparing NkkinParser, AngleSharp and HtmlAgilityPack across multiple workloads. Below are representative results (your mileage will vary by CPU, available SIMD support, and input documents).

| Benchmark | Library | Mean (ms) | Error (ms) | StdDev (ms) | Median (ms) | Allocated |
|---|---:|---:|---:|---:|---:|---:|
| Selector | AngleSharp | 1151.87 | 22.68 | 30.28 | 1132.40 | 257.78 KB |
| Selector | NkkinParser | 0.000575 | 0.00000185 | 0.00000164 | 0.000574 | 1.27 KB |
| Selector | HtmlAgilityPack | 361.60 | 5.46 | 4.84 | 361.60 | 24.27 MB |
| Parsing | AngleSharp | 185.21 | 6.45 | 19.01 | 185.21 | 77.24 MB |
| Parsing | NkkinParser | 39.87 | 1.05 | 3.08 | 39.87 | 70.53 MB |
| Streaming | NkkinParser (sync) | 22.11 | 0.44 | 1.02 | 22.17 | 51.49 MB |
| Streaming | NkkinParser (async) | 46.97 | 0.25 | 0.21 | 47.01 | 56.42 MB |

> High-level takeaway: NkkinParser consistently outperforms AngleSharp for parse throughput and selector queries while producing far fewer allocations in selector workloads. Selector times for NkkinParser assume a pre-built `DocumentIndex` (amortizes index build cost across repeated queries).

---

## Key features

- ⚡ SIMD-accelerated parsing and scanning (AVX2/AVX-512/Vector512)
- 🧠 Near-zero allocations: spans, stackalloc, arena-backed buffers
- 🔧 Full tolerant HTML5 parsing (quirks mode & robust recovery)
- 🔎 Compiled CSS selector engine with optional `DocumentIndex` for ultrafast queries
- 📡 Streaming `IAsyncEnumerable<Node>` parser for huge documents
- 🌐 Direct parsing from URLs / streams (auto-decompression handled externally)
- 🧪 BenchmarkDotNet suite included (Parsing, Selector, Streaming, Attribute workloads)
- ✅ Comprehensive unit tests

---

## Quick start — examples

Below are common usage patterns. All examples assume you reference the `NkkinParser` project or package and target .NET 10.

### 1) Full DOM parsing from a string

```csharp
using NkkinParser;

string html = await File.ReadAllTextAsync("page.html");
using var parser = new HtmlParser(html);
var document = parser.Parse();

var images = document.QuerySelectorAll("img[src]");
foreach (var img in images)
    Console.WriteLine(img.Attributes.Get("src"));
```

### 2) Direct from URL (fetch then parse)

```csharp
using var http = new HttpClient();
string html = await http.GetStringAsync("https://example.com/");
using var parser = new HtmlParser(html);
var doc = parser.Parse();
```

### 3) Streaming pull parsing (low memory)

```csharp
using var http = new HttpClient();
using var stream = await http.GetStreamAsync("https://example.com/large-page");
await foreach (var node in HtmlParser.ParseAsync(stream))
{
    if (node is Element e && e.TagName == "a")
        Console.WriteLine(e.Attributes.Get("href"));
}
```

### 4) Fast selectors with an index (amortize cost for repeated queries)

```csharp
using var parser = new HtmlParser(html);
var doc = parser.Parse();
using var arena = new ArenaAllocator();
var index = new DocumentIndex(doc, arena);

// Very fast queries when index is present
var results = doc.QuerySelectorAll("a[href], div, .class-name, #search", index);
```

---

## Installation

During development, reference the project directly:

```bash
dotnet add <your-project>.csproj reference ./NkkinParser/NkkinParser.csproj
```

Future: `dotnet add package NkkinParser` (NuGet) — planned.

Requirements: .NET 10 SDK. SIMD support (AVX2/AVX-512) improves throughput but is not required.

---

## Build & run benchmarks

1. Build in Release mode:

```bash
dotnet build -c Release
```

2. Run all benchmarks (BenchmarkDotNet will generate artifacts in `BenchmarkDotNet.Artifacts/results`):

```bash
dotnet run -c Release --project NkkinParser.Benchmarks/NkkinParser.Benchmarks.csproj -- *
```

3. Run a specific benchmark (example: SelectorBenchmark):

```bash
dotnet run -c Release --project NkkinParser.Benchmarks/NkkinParser.Benchmarks.csproj -- --filter *SelectorBenchmark*
```

Benchmark artifacts (CSV/HTML/MD) are written to `BenchmarkDotNet.Artifacts/results`.

---

## Roadmap

1. True incremental streaming tokenizer (support partial input/chunked tokenization)
2. Zero-string DOM mode (views over arena buffers to eliminate string allocation for node text)
3. SIMD-enhanced entity decode kernels
4. NuGet package publication + CI/CD
5. More compiled selector optimizations (parallel evaluation, richer indices)

---

## Contributing

Contributions are welcome — bugfixes, performance microbenchmarks, tests, docs, and feature PRs. Please:

1. Fork the repository and create a branch
2. Run `dotnet build` and `dotnet test`
3. Add benchmarks for performance changes
4. Open a PR with before/after data and a clear description

Be respectful and include reproducible evidence for performance claims.

---

## License

`NkkinParser` is MIT licensed — see the `LICENSE` file.

---

If you value speed and efficiency in .NET parsing, star this repo and contribute — together we can make .NET parsing fast again. ⭐