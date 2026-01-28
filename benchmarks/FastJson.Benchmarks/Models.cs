using System.Text.Json.Serialization;
using FastJson;

[assembly: FastJsonInclude(typeof(FastJson.Benchmarks.SimpleObject))]
[assembly: FastJsonInclude(typeof(FastJson.Benchmarks.ComplexObject))]
[assembly: FastJsonInclude(typeof(FastJson.Benchmarks.Address))]
[assembly: FastJsonInclude(typeof(FastJson.Benchmarks.LargeCollection))]

namespace FastJson.Benchmarks;

/// <summary>
/// STJ Source Generator context for benchmark comparison
/// </summary>
[JsonSerializable(typeof(SimpleObject))]
[JsonSerializable(typeof(ComplexObject))]
[JsonSerializable(typeof(Address))]
[JsonSerializable(typeof(LargeCollection))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Dictionary<string, int>))]
[JsonSerializable(typeof(List<SimpleObject>))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class BenchmarkJsonContext : JsonSerializerContext
{
}

public class SimpleObject
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
    public double Score { get; set; }
}

public class ComplexObject
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public Address Address { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, int> Metadata { get; set; } = new();
}

public class Address
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string Country { get; set; } = "";
    public string ZipCode { get; set; } = "";
}

public class LargeCollection
{
    public List<SimpleObject> Items { get; set; } = new();
}
