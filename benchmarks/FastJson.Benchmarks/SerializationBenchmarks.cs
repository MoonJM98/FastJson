using System.Text.Json;
using System.Text.Json.Serialization;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;

namespace FastJson.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class SerializationBenchmarks
{
    private SimpleObject _simpleObject = null!;
    private ComplexObject _complexObject = null!;
    private LargeCollection _largeCollection = null!;

    private string _simpleJson = null!;
    private string _complexJson = null!;
    private string _largeJson = null!;

    private JsonSerializerOptions _stjOptions = null!;

    [GlobalSetup]
    public void Setup()
    {
        _simpleObject = new SimpleObject
        {
            Id = 1,
            Name = "Test Object",
            IsActive = true,
            Score = 98.5
        };

        _complexObject = new ComplexObject
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            CreatedAt = DateTime.UtcNow,
            Address = new Address
            {
                Street = "123 Main St",
                City = "New York",
                Country = "USA",
                ZipCode = "10001"
            },
            Tags = new List<string> { "developer", "senior", "team-lead" },
            Metadata = new Dictionary<string, int>
            {
                ["projects"] = 15,
                ["commits"] = 1250,
                ["reviews"] = 340
            }
        };

        _largeCollection = new LargeCollection
        {
            Items = Enumerable.Range(1, 1000).Select(i => new SimpleObject
            {
                Id = i,
                Name = $"Object {i}",
                IsActive = i % 2 == 0,
                Score = i * 1.5
            }).ToList()
        };

        _stjOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Pre-serialize for deserialization benchmarks
        _simpleJson = FastJson.Serialize(_simpleObject);
        _complexJson = FastJson.Serialize(_complexObject);
        _largeJson = FastJson.Serialize(_largeCollection);
    }

    // ===== Simple Object Serialization =====

    [Benchmark]
    [BenchmarkCategory("Simple", "Serialize")]
    public string STJ_Reflection_Serialize_Simple()
    {
        return JsonSerializer.Serialize(_simpleObject, _stjOptions);
    }

    [Benchmark]
    [BenchmarkCategory("Simple", "Serialize")]
    public string STJ_SourceGen_Serialize_Simple()
    {
        return JsonSerializer.Serialize(_simpleObject, BenchmarkJsonContext.Default.SimpleObject);
    }

    [Benchmark]
    [BenchmarkCategory("Simple", "Serialize")]
    public string FastJson_Serialize_Simple()
    {
        return FastJson.Serialize(_simpleObject);
    }

    // ===== Simple Object Deserialization =====

    [Benchmark]
    [BenchmarkCategory("Simple", "Deserialize")]
    public SimpleObject? STJ_Reflection_Deserialize_Simple()
    {
        return JsonSerializer.Deserialize<SimpleObject>(_simpleJson, _stjOptions);
    }

    [Benchmark]
    [BenchmarkCategory("Simple", "Deserialize")]
    public SimpleObject? STJ_SourceGen_Deserialize_Simple()
    {
        return JsonSerializer.Deserialize(_simpleJson, BenchmarkJsonContext.Default.SimpleObject);
    }

    [Benchmark]
    [BenchmarkCategory("Simple", "Deserialize")]
    public SimpleObject? FastJson_Deserialize_Simple()
    {
        return FastJson.Deserialize<SimpleObject>(_simpleJson);
    }

    // ===== Complex Object Serialization =====

    [Benchmark]
    [BenchmarkCategory("Complex", "Serialize")]
    public string STJ_Reflection_Serialize_Complex()
    {
        return JsonSerializer.Serialize(_complexObject, _stjOptions);
    }

    [Benchmark]
    [BenchmarkCategory("Complex", "Serialize")]
    public string STJ_SourceGen_Serialize_Complex()
    {
        return JsonSerializer.Serialize(_complexObject, BenchmarkJsonContext.Default.ComplexObject);
    }

    [Benchmark]
    [BenchmarkCategory("Complex", "Serialize")]
    public string FastJson_Serialize_Complex()
    {
        return FastJson.Serialize(_complexObject);
    }

    // ===== Complex Object Deserialization =====

    [Benchmark]
    [BenchmarkCategory("Complex", "Deserialize")]
    public ComplexObject? STJ_Reflection_Deserialize_Complex()
    {
        return JsonSerializer.Deserialize<ComplexObject>(_complexJson, _stjOptions);
    }

    [Benchmark]
    [BenchmarkCategory("Complex", "Deserialize")]
    public ComplexObject? STJ_SourceGen_Deserialize_Complex()
    {
        return JsonSerializer.Deserialize(_complexJson, BenchmarkJsonContext.Default.ComplexObject);
    }

    [Benchmark]
    [BenchmarkCategory("Complex", "Deserialize")]
    public ComplexObject? FastJson_Deserialize_Complex()
    {
        return FastJson.Deserialize<ComplexObject>(_complexJson);
    }

    // ===== Large Collection Serialization =====

    [Benchmark]
    [BenchmarkCategory("Large", "Serialize")]
    public string STJ_Reflection_Serialize_Large()
    {
        return JsonSerializer.Serialize(_largeCollection, _stjOptions);
    }

    [Benchmark]
    [BenchmarkCategory("Large", "Serialize")]
    public string STJ_SourceGen_Serialize_Large()
    {
        return JsonSerializer.Serialize(_largeCollection, BenchmarkJsonContext.Default.LargeCollection);
    }

    [Benchmark]
    [BenchmarkCategory("Large", "Serialize")]
    public string FastJson_Serialize_Large()
    {
        return FastJson.Serialize(_largeCollection);
    }

    // ===== Large Collection Deserialization =====

    [Benchmark]
    [BenchmarkCategory("Large", "Deserialize")]
    public LargeCollection? STJ_Reflection_Deserialize_Large()
    {
        return JsonSerializer.Deserialize<LargeCollection>(_largeJson, _stjOptions);
    }

    [Benchmark]
    [BenchmarkCategory("Large", "Deserialize")]
    public LargeCollection? STJ_SourceGen_Deserialize_Large()
    {
        return JsonSerializer.Deserialize(_largeJson, BenchmarkJsonContext.Default.LargeCollection);
    }

    [Benchmark]
    [BenchmarkCategory("Large", "Deserialize")]
    public LargeCollection? FastJson_Deserialize_Large()
    {
        return FastJson.Deserialize<LargeCollection>(_largeJson);
    }
}
