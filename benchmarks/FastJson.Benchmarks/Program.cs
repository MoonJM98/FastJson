using BenchmarkDotNet.Running;
using FastJson.Benchmarks;

// Quick benchmark for fast comparison
if (args.Length > 0 && args[0] == "--quick")
{
    QuickBenchmark.Run();
    return;
}

// Full BenchmarkDotNet run
BenchmarkRunner.Run<SerializationBenchmarks>();
