using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using FastJson.Generator;

namespace FastJson.Tests;

public class GeneratorTests
{
    [Fact]
    public void Generator_SimpleSerialize_GeneratesCode()
    {
        // Arrange
        var source = @"
using FastJson;

public class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
}

public class Program
{
    public static void Main()
    {
        var person = new Person();
        var json = FastJson.FastJson.Serialize(person);
    }
}
";

        // Act
        var (diagnostics, outputCompilation) = RunGenerator(source);

        // Assert
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedTrees = outputCompilation.SyntaxTrees
            .Where(t => t.FilePath.Contains(".g.cs"))
            .ToList();

        // Now only one file is generated (FastJsonContext.g.cs) that contains both the resolver and impl
        Assert.Single(generatedTrees);

        var contextCode = generatedTrees.First(t => t.FilePath.Contains("FastJsonContext")).ToString();

        // Check for v2 generated code patterns
        Assert.Contains("FastJsonProps", contextCode);  // Pre-encoded property names
        Assert.Contains("PersonSerializer", contextCode);  // Type-specific serializer
        Assert.Contains("PersonDeserializer", contextCode);  // Type-specific deserializer
        Assert.Contains("FastJsonContext", contextCode);  // Context class
        Assert.Contains("IFastJsonContext", contextCode);  // Implements interface
        Assert.Contains("TryGetWriter", contextCode);  // Writer lookup method
        Assert.Contains("TryGetReader", contextCode);  // Reader lookup method
        Assert.Contains("RegisterContext", contextCode);  // Context registration in initializer
    }

    [Fact]
    public void Generator_MultipleTypes_GeneratesAll()
    {
        // Arrange
        var source = @"
using FastJson;

public class User { public string Name { get; set; } }
public class Order { public int Id { get; set; } }

public class Program
{
    public static void Main()
    {
        FastJson.FastJson.Serialize(new User());
        FastJson.FastJson.Serialize(new Order());
    }
}
";

        // Act
        var (diagnostics, outputCompilation) = RunGenerator(source);

        // Assert
        var contextTree = outputCompilation.SyntaxTrees
            .First(t => t.FilePath.Contains("FastJsonContext"));
        var code = contextTree.ToString();

        Assert.Contains("User", code);
        Assert.Contains("Order", code);
    }

    [Fact]
    public void Generator_FastJsonIncludeAttribute_IncludesType()
    {
        // Arrange
        var source = @"
using FastJson;

[assembly: FastJsonInclude(typeof(ExternalType))]

public class ExternalType
{
    public string Value { get; set; }
}

public class Program
{
    public static void Main() { }
}
";

        // Act
        var (diagnostics, outputCompilation) = RunGenerator(source);

        // Assert
        var contextTree = outputCompilation.SyntaxTrees
            .FirstOrDefault(t => t.FilePath.Contains("FastJsonContext"));

        Assert.NotNull(contextTree);
        Assert.Contains("ExternalType", contextTree.ToString());
    }

    [Fact]
    public void Generator_NestedTypes_CollectsPropertyTypes()
    {
        // Arrange
        var source = @"
using FastJson;

public class Order
{
    public Customer Customer { get; set; }
}

public class Customer
{
    public string Name { get; set; }
}

public class Program
{
    public static void Main()
    {
        FastJson.FastJson.Serialize(new Order());
    }
}
";

        // Act
        var (_, outputCompilation) = RunGenerator(source);

        // Assert
        var contextTree = outputCompilation.SyntaxTrees
            .First(t => t.FilePath.Contains("FastJsonContext"));
        var code = contextTree.ToString();

        Assert.Contains("Order", code);
        Assert.Contains("Customer", code);
    }

    [Fact]
    public void Generator_NoUsage_GeneratesNothing()
    {
        // Arrange
        var source = @"
public class Person
{
    public string Name { get; set; }
}

public class Program
{
    public static void Main() { }
}
";

        // Act
        var (_, outputCompilation) = RunGenerator(source);

        // Assert
        var generatedTrees = outputCompilation.SyntaxTrees
            .Where(t => t.FilePath.Contains(".g.cs"))
            .ToList();

        Assert.Empty(generatedTrees);
    }

    [Fact]
    public void Generator_GenericWrapperWithConcreteType_CollectsInnerType()
    {
        // Arrange - Wrapper<T>가 내부에서 Deserialize<T>를 호출하고,
        // 외부에서 Wrapper<List<Person>>으로 사용될 때 List<Person>, Person이 수집되어야 함
        var source = @"
using FastJson;
using System.Collections.Generic;

public class Person { public string Name { get; set; } }

public class Wrapper<T>
{
    public T? Get(string json) => FastJson.FastJson.Deserialize<T>(json);
    public string Set(T value) => FastJson.FastJson.Serialize(value);
}

public class Program
{
    public static void Main()
    {
        var wrapper = new Wrapper<List<Person>>();
        var people = wrapper.Get(""[]"");
        var json = wrapper.Set(new List<Person>());
    }
}
";

        // Act
        var (diagnostics, outputCompilation) = RunGenerator(source);

        // Assert
        var contextTree = outputCompilation.SyntaxTrees
            .FirstOrDefault(t => t.FilePath.Contains("FastJsonContext"));

        Assert.NotNull(contextTree);
        var code = contextTree.ToString();

        // List<Person>과 Person이 수집되어야 함
        Assert.Contains("Person", code);
        Assert.Contains("List", code);
    }

    [Fact]
    public void Generator_NestedGenericWrapper_CollectsAllTypes()
    {
        // Arrange - Wrapper<Wrapper<T>> 같은 중첩 제네릭
        var source = @"
using FastJson;

public class Data { public int Value { get; set; } }

public class Box<T>
{
    public T Content { get; set; }
    public string ToJson() => FastJson.FastJson.Serialize(this);
}

public class Program
{
    public static void Main()
    {
        var box = new Box<Box<Data>>();
        var json = box.ToJson();
    }
}
";

        // Act
        var (diagnostics, outputCompilation) = RunGenerator(source);

        // Assert
        var contextTree = outputCompilation.SyntaxTrees
            .FirstOrDefault(t => t.FilePath.Contains("FastJsonContext"));

        Assert.NotNull(contextTree);
        var code = contextTree.ToString();

        // Box<Box<Data>>와 Box<Data>, Data가 모두 수집되어야 함
        Assert.Contains("Data", code);
        Assert.Contains("Box", code);
    }

    [Fact]
    public void Generator_GenericMethodInvocation_CollectsTypes()
    {
        // Arrange - WriteJson<T>() 메서드가 내부에서 FastJson.Serialize<T>()를 호출하고,
        // 외부에서 WriteJson<Person>()으로 사용될 때 Person이 수집되어야 함
        var source = @"
using FastJson;

public class Person { public string Name { get; set; } }

public class Service
{
    public string WriteJson<T>(T value) => FastJson.FastJson.Serialize(value);
    public T? ReadJson<T>(string json) => FastJson.FastJson.Deserialize<T>(json);
}

public class Program
{
    public static void Main()
    {
        var service = new Service();
        var json = service.WriteJson<Person>(new Person());
        var person = service.ReadJson<Person>(""{}"");
    }
}
";

        // Act
        var (diagnostics, outputCompilation) = RunGenerator(source);

        // Assert
        var contextTree = outputCompilation.SyntaxTrees
            .FirstOrDefault(t => t.FilePath.Contains("FastJsonContext"));

        Assert.NotNull(contextTree);
        var code = contextTree.ToString();

        // Person이 수집되어야 함
        Assert.Contains("Person", code);
    }

    [Fact]
    public void Generator_GenericMethodWithListType_CollectsInnerType()
    {
        // Arrange - WriteJson<List<Person>>() 호출 시 Person도 수집되어야 함
        var source = @"
using FastJson;
using System.Collections.Generic;

public class Person { public string Name { get; set; } }

public class Service
{
    public string WriteJson<T>(T value) => FastJson.FastJson.Serialize(value);
}

public class Program
{
    public static void Main()
    {
        var service = new Service();
        var json = service.WriteJson<List<Person>>(new List<Person>());
    }
}
";

        // Act
        var (diagnostics, outputCompilation) = RunGenerator(source);

        // Assert
        var contextTree = outputCompilation.SyntaxTrees
            .FirstOrDefault(t => t.FilePath.Contains("FastJsonContext"));

        Assert.NotNull(contextTree);
        var code = contextTree.ToString();

        // List<Person>과 Person이 수집되어야 함
        Assert.Contains("Person", code);
        Assert.Contains("List", code);
    }

    [Fact]
    public void Generator_AsyncGenericMethod_CollectsTypes()
    {
        // Arrange - WriteJsonAsync<T>() 비동기 메서드도 추적해야 함
        var source = @"
using FastJson;
using System.IO;
using System.Threading.Tasks;

public class Person { public string Name { get; set; } }

public class Service
{
    public async Task WriteJsonAsync<T>(Stream stream, T value)
    {
        await FastJson.FastJson.SerializeAsync(stream, value);
    }
}

public class Program
{
    public static async Task Main()
    {
        var service = new Service();
        using var stream = new MemoryStream();
        await service.WriteJsonAsync<Person>(stream, new Person());
    }
}
";

        // Act
        var (diagnostics, outputCompilation) = RunGenerator(source);

        // Assert
        var contextTree = outputCompilation.SyntaxTrees
            .FirstOrDefault(t => t.FilePath.Contains("FastJsonContext"));

        Assert.NotNull(contextTree);
        var code = contextTree.ToString();

        // Person이 수집되어야 함
        Assert.Contains("Person", code);
    }

    [Fact]
    public void Generator_IReadOnlyList_GeneratesListInstantiation()
    {
        // Arrange
        var source = @"
using FastJson;
using System.Collections.Generic;

public class AuditEntry { public string Message { get; set; } }
public class AuditLog
{
    public IReadOnlyList<AuditEntry> Entries { get; set; }
}

public class Program
{
    public static void Main()
    {
        FastJson.FastJson.Serialize(new AuditLog());
        FastJson.FastJson.Deserialize<AuditLog>(""{}"");
    }
}
";

        // Act
        var (diagnostics, outputCompilation) = RunGenerator(source);

        // Assert - No compilation errors
        var errors = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(errors);

        // Check generated code uses List<T> for instantiation
        var contextTree = outputCompilation.SyntaxTrees
            .First(t => t.FilePath.Contains("FastJsonContext"));
        var code = contextTree.ToString();

        Assert.Contains("new System.Collections.Generic.List<", code);
    }

    [Fact]
    public void Generator_IDictionary_GeneratesDictionaryInstantiation()
    {
        // Arrange
        var source = @"
using FastJson;
using System.Collections.Generic;

public class Config
{
    public IDictionary<string, string> Settings { get; set; }
    public IReadOnlyDictionary<string, int> Values { get; set; }
}

public class Program
{
    public static void Main()
    {
        FastJson.FastJson.Serialize(new Config());
        FastJson.FastJson.Deserialize<Config>(""{}"");
    }
}
";

        // Act
        var (diagnostics, outputCompilation) = RunGenerator(source);

        // Assert - No compilation errors
        var errors = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(errors);

        var contextTree = outputCompilation.SyntaxTrees
            .First(t => t.FilePath.Contains("FastJsonContext"));
        var code = contextTree.ToString();

        // Check generated code uses Dictionary<K,V> for instantiation
        Assert.Contains("new System.Collections.Generic.Dictionary<", code);
    }

    private static (ImmutableArray<Diagnostic>, Compilation) RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Text.Json.JsonSerializer).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(global::FastJson.FastJsonOptionsAttribute).Assembly.Location)
        };

        // Add runtime references
        var assemblyPath = System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        references.Add(MetadataReference.CreateFromFile(System.IO.Path.Combine(assemblyPath, "System.Runtime.dll")));
        references.Add(MetadataReference.CreateFromFile(System.IO.Path.Combine(assemblyPath, "System.Collections.dll")));
        references.Add(MetadataReference.CreateFromFile(System.IO.Path.Combine(assemblyPath, "System.Memory.dll")));
        references.Add(MetadataReference.CreateFromFile(typeof(System.Buffers.IBufferWriter<>).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(System.Text.Encodings.Web.JavaScriptEncoder).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));

        var generator = new FastJsonGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);

        return (diagnostics, outputCompilation);
    }
}
