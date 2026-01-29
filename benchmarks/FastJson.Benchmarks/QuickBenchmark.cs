using System.Diagnostics;
using System.Text.Json;
using InlineFastJson = FastJson.Generated.FastJson_Benchmarks.FastJson;

namespace FastJson.Benchmarks;

public static class QuickBenchmark
{
    public static void Run()
    {
        const int warmup = 1000;
        const int iterations = 100000;

        var simple = new SimpleObject { Id = 1, Name = "Test", IsActive = true, Score = 98.5 };
        var simpleJson = JsonSerializer.Serialize(simple, BenchmarkJsonContext.Default.SimpleObject);

        var reflectionOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        Console.WriteLine("=== Quick Benchmark (Simple Object) ===\n");
        Console.WriteLine($"Iterations: {iterations:N0}\n");

        // Warmup
        for (int i = 0; i < warmup; i++)
        {
            _ = JsonSerializer.Serialize(simple, reflectionOptions);
            _ = JsonSerializer.Serialize(simple, BenchmarkJsonContext.Default.SimpleObject);
            _ = global::FastJson.FastJson.Serialize(simple);
            _ = InlineFastJson.Serialize(simple);
        }

        // STJ Reflection - Serialize
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
            _ = JsonSerializer.Serialize(simple, reflectionOptions);
        sw.Stop();
        var stjReflectionSerialize = sw.Elapsed.TotalNanoseconds / iterations;

        // STJ SourceGen - Serialize
        sw.Restart();
        for (int i = 0; i < iterations; i++)
            _ = JsonSerializer.Serialize(simple, BenchmarkJsonContext.Default.SimpleObject);
        sw.Stop();
        var stjSourceGenSerialize = sw.Elapsed.TotalNanoseconds / iterations;

        // FastJson (delegate) - Serialize
        sw.Restart();
        for (int i = 0; i < iterations; i++)
            _ = global::FastJson.FastJson.Serialize(simple);
        sw.Stop();
        var fastJsonSerialize = sw.Elapsed.TotalNanoseconds / iterations;

        // FastJson Inline - Serialize
        sw.Restart();
        for (int i = 0; i < iterations; i++)
            _ = InlineFastJson.Serialize(simple);
        sw.Stop();
        var fastJsonInlineSerialize = sw.Elapsed.TotalNanoseconds / iterations;

        // Warmup Deserialize
        for (int i = 0; i < warmup; i++)
        {
            _ = JsonSerializer.Deserialize<SimpleObject>(simpleJson, reflectionOptions);
            _ = JsonSerializer.Deserialize(simpleJson, BenchmarkJsonContext.Default.SimpleObject);
            _ = global::FastJson.FastJson.Deserialize<SimpleObject>(simpleJson);
            _ = InlineFastJson.Deserialize<SimpleObject>(simpleJson);
        }

        // STJ Reflection - Deserialize
        sw.Restart();
        for (int i = 0; i < iterations; i++)
            _ = JsonSerializer.Deserialize<SimpleObject>(simpleJson, reflectionOptions);
        sw.Stop();
        var stjReflectionDeserialize = sw.Elapsed.TotalNanoseconds / iterations;

        // STJ SourceGen - Deserialize
        sw.Restart();
        for (int i = 0; i < iterations; i++)
            _ = JsonSerializer.Deserialize(simpleJson, BenchmarkJsonContext.Default.SimpleObject);
        sw.Stop();
        var stjSourceGenDeserialize = sw.Elapsed.TotalNanoseconds / iterations;

        // FastJson (delegate) - Deserialize
        sw.Restart();
        for (int i = 0; i < iterations; i++)
            _ = global::FastJson.FastJson.Deserialize<SimpleObject>(simpleJson);
        sw.Stop();
        var fastJsonDeserialize = sw.Elapsed.TotalNanoseconds / iterations;

        // FastJson Inline - Deserialize
        sw.Restart();
        for (int i = 0; i < iterations; i++)
            _ = InlineFastJson.Deserialize<SimpleObject>(simpleJson);
        sw.Stop();
        var fastJsonInlineDeserialize = sw.Elapsed.TotalNanoseconds / iterations;

        // Print results
        Console.WriteLine("SERIALIZE (SimpleObject):");
        Console.WriteLine($"  STJ Reflection:    {stjReflectionSerialize,8:F1} ns/op");
        Console.WriteLine($"  STJ SourceGen:     {stjSourceGenSerialize,8:F1} ns/op  ({stjSourceGenSerialize / stjReflectionSerialize:P0} of Reflection)");
        Console.WriteLine($"  FastJson Delegate: {fastJsonSerialize,8:F1} ns/op  ({fastJsonSerialize / stjReflectionSerialize:P0} of Reflection)");
        Console.WriteLine($"  FastJson Inline:   {fastJsonInlineSerialize,8:F1} ns/op  ({fastJsonInlineSerialize / stjReflectionSerialize:P0} of Reflection)");
        Console.WriteLine();
        Console.WriteLine("DESERIALIZE (SimpleObject):");
        Console.WriteLine($"  STJ Reflection:    {stjReflectionDeserialize,8:F1} ns/op");
        Console.WriteLine($"  STJ SourceGen:     {stjSourceGenDeserialize,8:F1} ns/op  ({stjSourceGenDeserialize / stjReflectionDeserialize:P0} of Reflection)");
        Console.WriteLine($"  FastJson Delegate: {fastJsonDeserialize,8:F1} ns/op  ({fastJsonDeserialize / stjReflectionDeserialize:P0} of Reflection)");
        Console.WriteLine($"  FastJson Inline:   {fastJsonInlineDeserialize,8:F1} ns/op  ({fastJsonInlineDeserialize / stjReflectionDeserialize:P0} of Reflection)");
        Console.WriteLine();

        // FastJson overhead
        Console.WriteLine("FastJson Inline vs STJ SourceGen:");
        var serializeOverhead = (fastJsonInlineSerialize - stjSourceGenSerialize) / stjSourceGenSerialize * 100;
        var deserializeOverhead = (fastJsonInlineDeserialize - stjSourceGenDeserialize) / stjSourceGenDeserialize * 100;
        Console.WriteLine($"  Serialize:   {serializeOverhead:+0.0;-0.0}%");
        Console.WriteLine($"  Deserialize: {deserializeOverhead:+0.0;-0.0}%");

        Console.WriteLine();
        Console.WriteLine("FastJson Inline vs Delegate:");
        var inlineVsDelegateSerialize = (fastJsonInlineSerialize - fastJsonSerialize) / fastJsonSerialize * 100;
        var inlineVsDelegateDeserialize = (fastJsonInlineDeserialize - fastJsonDeserialize) / fastJsonDeserialize * 100;
        Console.WriteLine($"  Serialize:   {inlineVsDelegateSerialize:+0.0;-0.0}%");
        Console.WriteLine($"  Deserialize: {inlineVsDelegateDeserialize:+0.0;-0.0}%");
    }
}
