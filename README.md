# FastJson

Zero-configuration AOT-compatible JSON serializer for .NET, built on System.Text.Json source generators.

## Features

- **Zero Configuration**: Automatically detects types from `FastJson.Serialize<T>()` and `FastJson.Deserialize<T>()` calls
- **AOT Compatible**: Full Native AOT support with compile-time code generation
- **High Performance**: Direct Utf8JsonWriter calls with ArrayPool-based buffer pooling
- **Incremental Generator**: Fast rebuild times with smart caching
- **Rich Attribute Support**: `[JsonPropertyName]`, `[JsonIgnore]`, `[JsonConstructor]`, `[JsonInclude]`, `[JsonPolymorphic]`, `[JsonNaming]`
- **Flexible Naming**: SnakeCase, KebabCase, CamelCase with case-insensitive matching
- **Dynamic JSON**: JsonNode property support for mixed static/dynamic data
- **Comprehensive Analyzer**: Compile-time diagnostics for common mistakes

## Installation

```bash
dotnet add package MoonJM98.FastJson
```

## Quick Start

```csharp
using FastJson;

// Define your types
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}

// Serialize
var user = new User { Id = 1, Name = "John", Email = "john@example.com" };
string json = FastJson.Serialize(user);
// Output: {"id":1,"name":"John","email":"john@example.com"}

// Deserialize
User? deserialized = FastJson.Deserialize<User>(json);
```

That's it! No manual type registration, no attributes required for basic usage.

## API Reference

### Synchronous Methods

```csharp
// Serialize object to JSON string
string json = FastJson.Serialize<T>(T value);

// Serialize object to UTF-8 byte array
byte[] bytes = FastJson.SerializeToUtf8Bytes<T>(T value);

// Serialize object to pre-allocated span (returns bytes written)
int written = FastJson.Serialize<T>(T value, Span<byte> destination);

// Deserialize JSON string to object
T? obj = FastJson.Deserialize<T>(string json);

// Deserialize UTF-8 bytes to object
T? obj = FastJson.Deserialize<T>(ReadOnlySpan<byte> utf8Json);

// Deserialize to dynamic JsonNode
JsonNode? node = FastJson.Deserialize(string json);
JsonNode? node = FastJson.Deserialize(ReadOnlySpan<byte> utf8Json);
```

### Asynchronous Methods

```csharp
// Serialize to stream
await FastJson.SerializeAsync<T>(Stream stream, T value, CancellationToken ct = default);

// Deserialize from stream
T? obj = await FastJson.DeserializeAsync<T>(Stream stream, CancellationToken ct = default);
```

## Configuration

### Assembly-Level Options

Configure serialization behavior using the `[FastJsonOptions]` attribute:

```csharp
[assembly: FastJsonOptions(
    PropertyNamingPolicy = "CamelCase",  // CamelCase, PascalCase, SnakeCaseLower, SnakeCaseUpper, KebabCaseLower, KebabCaseUpper
    WriteIndented = false,
    IgnoreReadOnlyProperties = false,
    DefaultIgnoreCondition = false,      // Ignore properties with default values
    PropertyNameCaseInsensitive = false,
    AllowTrailingCommas = false,
    ReadCommentHandling = false
)]
```

### Including External Types

For types from external assemblies (NuGet packages, other projects), use `[FastJsonInclude]`:

```csharp
[assembly: FastJsonInclude(typeof(ExternalLibrary.SomeDto))]
[assembly: FastJsonInclude(typeof(AnotherLibrary.AnotherType))]
```

## Supported Attributes

FastJson respects standard System.Text.Json attributes:

### `[JsonPropertyName]`

Customize the JSON property name:

```csharp
public class Product
{
    [JsonPropertyName("product_id")]
    public int Id { get; set; }

    [JsonPropertyName("display_name")]
    public string Name { get; set; }
}
// Output: {"product_id":1,"display_name":"Widget"}
```

### `[JsonIgnore]`

Exclude properties from serialization:

```csharp
public class User
{
    public string Username { get; set; }

    [JsonIgnore]
    public string Password { get; set; }  // Never serialized
}
```

### `[JsonConstructor]`

Specify a constructor for deserialization:

```csharp
public class ImmutablePerson
{
    public string Name { get; }
    public int Age { get; }

    [JsonConstructor]
    public ImmutablePerson(string name, int age)
    {
        Name = name;
        Age = age;
    }
}
```

### `[JsonInclude]`

Include non-public properties or fields:

```csharp
public class Config
{
    [JsonInclude]
    private string _internalId = "default";

    [JsonInclude]
    internal int InternalValue { get; set; }
}
```

### `[JsonNumberHandling]`

Control number serialization behavior:

```csharp
public class Metrics
{
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int Count { get; set; }  // Can deserialize "123" as 123
}
```

### `[JsonConverter]`

Use custom converters:

```csharp
public class Event
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EventType Type { get; set; }  // Serializes as "Created" instead of 0
}
```

## JsonNaming Support

FastJson provides flexible property naming with the `[JsonNaming]` attribute:

### NamingPolicy

```csharp
using FastJson;

// SnakeCase: UserName -> user_name
[JsonNaming(NamingPolicy.SnakeCase)]
public class SnakeCaseModel
{
    public string UserName { get; set; }
    public int UserId { get; set; }
}
// Output: {"user_name":"john","user_id":123}

// KebabCase: UserName -> user-name
[JsonNaming(NamingPolicy.KebabCase)]
public class KebabCaseModel
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
}
// Output: {"first-name":"John","last-name":"Doe"}
```

### Flexible Deserialization

Enable case-insensitive or special-character-ignoring matching:

```csharp
// IgnoreCase: matches "USERNAME", "UserName", "username"
[JsonNaming(NamingPolicy.CamelCase, IgnoreCase = true)]
public class FlexibleModel
{
    public string UserName { get; set; }
}

// IgnoreSpecialCharacters: matches "user_name", "user-name", "userName"
[JsonNaming(NamingPolicy.CamelCase, IgnoreSpecialCharacters = true)]
public class VeryFlexibleModel
{
    public string UserName { get; set; }
}

// Both work:
var json1 = "{\"USERNAME\":\"Alice\"}";
var json2 = "{\"user_name\":\"Bob\"}";
```

## JsonNode Property Support

Mix static typing with dynamic JSON data:

```csharp
using System.Text.Json.Nodes;

public class DynamicModel
{
    public string Name { get; set; }
    public JsonNode? Extra { get; set; }  // Dynamic JSON data
}

var model = new DynamicModel
{
    Name = "Test",
    Extra = JsonNode.Parse("{\"foo\":\"bar\",\"count\":123}")
};

string json = FastJson.Serialize(model);
// {"name":"Test","extra":{"foo":"bar","count":123}}

var restored = FastJson.Deserialize<DynamicModel>(json);
string foo = restored.Extra["foo"].GetValue<string>(); // "bar"
```

## Polymorphism Support

FastJson supports polymorphic serialization with `[JsonPolymorphic]` and `[JsonDerivedType]`:

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Dog), "dog")]
[JsonDerivedType(typeof(Cat), "cat")]
public abstract class Animal
{
    public string Name { get; set; }
}

public class Dog : Animal
{
    public string Breed { get; set; }
}

public class Cat : Animal
{
    public bool IsIndoor { get; set; }
}

// Usage
Animal pet = new Dog { Name = "Buddy", Breed = "Golden Retriever" };
string json = FastJson.Serialize(pet);
// Output: {"$type":"dog","name":"Buddy","breed":"Golden Retriever"}

Animal? restored = FastJson.Deserialize<Animal>(json);
// restored is Dog with correct properties
```

## Records Support

FastJson fully supports C# records:

```csharp
public record Person(string Name, int Age);

public record Product
{
    public int Id { get; init; }
    public string Name { get; init; }
    public decimal Price { get; init; }
}

var person = new Person("Alice", 30);
string json = FastJson.Serialize(person);
Person? restored = FastJson.Deserialize<Person>(json);
```

## Analyzer Diagnostics

FastJson includes a Roslyn analyzer that catches common mistakes at compile time:

| Code | Severity | Description |
|------|----------|-------------|
| FJ001 | Error | Generic type parameter not allowed. Use concrete types. |
| FJ002 | Error | Unsupported type (object, dynamic, delegates, pointers). |
| FJ003 | Warning | Type nesting depth exceeded (max 20 levels). |
| FJ004 | Warning | Type count exceeded (max 500 types). |
| FJ005 | Warning | Circular reference detected. |
| FJ006 | Info | External type from another assembly detected. |

### Example: FJ001

```csharp
// Error FJ001: FastJson cannot use type parameter 'T'. Use a concrete type instead.
public void SendData<T>(T data)
{
    string json = FastJson.Serialize(data);  // FJ001 error
}

// Fix: Use concrete types
public void SendUser(User user)
{
    string json = FastJson.Serialize(user);  // OK
}
```

### Example: FJ002

```csharp
// Error FJ002: Type 'object' is not supported
FastJson.Serialize<object>(someValue);  // FJ002 error

// Fix: Use specific type
FastJson.Serialize<User>(user);  // OK
```

## AOT Deployment

FastJson is fully compatible with .NET Native AOT:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <PublishAot>true</PublishAot>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MoonJM98.FastJson" Version="0.0.0.6" />
  </ItemGroup>
</Project>
```

```bash
dotnet publish -c Release -r win-x64
```

## How It Works

1. **Compile Time**: The source generator scans your code for `FastJson.Serialize<T>()` and `FastJson.Deserialize<T>()` calls
2. **Type Collection**: Recursively collects all types including nested properties, generic arguments, and collection elements
3. **Code Generation**: Generates a `JsonSerializerContext` with `[JsonSerializable]` attributes for each type
4. **Module Initializer**: Automatically configures FastJson at application startup

Generated code example:

```csharp
// Auto-generated FastJsonContext.g.cs
[JsonSerializable(typeof(User))]
[JsonSerializable(typeof(List<User>))]
internal partial class FastJsonContext : JsonSerializerContext { }
```

## Limitations

- Type arguments must be known at compile time (no `FastJson.Serialize<T>()` with open generic `T`)
- Unsupported types: `object`, `dynamic`, anonymous types, delegates, pointers, `Span<T>`
- For dynamic data, use `JsonNode` properties instead of anonymous types
- Maximum 500 types per compilation unit
- Maximum 20 levels of type nesting

## Comparison with System.Text.Json

| Feature | System.Text.Json | FastJson |
|---------|------------------|----------|
| Configuration | Manual JsonSerializerContext | Zero configuration |
| Type Registration | `[JsonSerializable]` per type | Automatic detection |
| AOT Support | Yes (with manual setup) | Yes (automatic) |
| Performance | Excellent | Excellent (same backend) |
| Learning Curve | Moderate | Minimal |

## Requirements

- .NET 8.0 or later
- C# 12 or later (for source generators)

## License

MIT License

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.
