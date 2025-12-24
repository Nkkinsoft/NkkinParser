using BenchmarkDotNet.Running;
using NkkinParser.Benchmarks.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(ParsingBenchmark).Assembly).Run(args);