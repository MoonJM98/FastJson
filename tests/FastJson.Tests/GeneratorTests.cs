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

    [Fact]
    public void Generator_FastJsonMethodAttribute_CollectsTypeFromCrossAssemblyCall()
    {
        // Arrange - [FastJsonMethod] 속성이 붙은 제네릭 메서드 호출 시
        // 타입 인자가 자동으로 등록되어야 함
        var source = @"
using FastJson;

public class TestValueDto { public string Value { get; set; } }

// 라이브러리에서 제공하는 유틸리티 클래스 (다른 어셈블리라고 가정)
public class ServerSentEvent
{
    [FastJsonMethod]
    public static string CreateJson<T>(T data) => FastJson.FastJson.Serialize(data);

    [FastJsonMethod]
    public static T? ParseJson<T>(string json) => FastJson.FastJson.Deserialize<T>(json);
}

public class Program
{
    public static void Main()
    {
        // CreateJson<TestValueDto>() 호출 - [FastJsonMethod] 덕분에 TestValueDto 등록됨
        var json = ServerSentEvent.CreateJson(new TestValueDto());
        var dto = ServerSentEvent.ParseJson<TestValueDto>(""{}"");
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

        // TestValueDto가 수집되어야 함
        Assert.Contains("TestValueDto", code);
    }

    [Fact]
    public void Generator_MultipleFastJsonInclude_CollectsAllTypes()
    {
        // Arrange - 여러 [assembly: FastJsonInclude] 속성이 모두 처리되어야 함
        var source = @"
using FastJson;

[assembly: FastJsonInclude(typeof(TypeA))]
[assembly: FastJsonInclude(typeof(TypeB))]
[assembly: FastJsonInclude(typeof(TypeC))]

public class TypeA { public string A { get; set; } }
public class TypeB { public string B { get; set; } }
public class TypeC { public string C { get; set; } }

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
        var code = contextTree.ToString();

        // 모든 타입이 수집되어야 함
        Assert.Contains("TypeA", code);
        Assert.Contains("TypeB", code);
        Assert.Contains("TypeC", code);
    }

    [Fact]
    public void Generator_FastJsonMethodAttribute_CollectsNestedGenericTypes()
    {
        // Arrange - [FastJsonMethod] 속성이 붙은 메서드에 List<Person> 등 중첩 제네릭 전달 시
        var source = @"
using FastJson;
using System.Collections.Generic;

public class Person { public string Name { get; set; } }

public class JsonHelper
{
    [FastJsonMethod]
    public static string ToJson<T>(T value) => FastJson.FastJson.Serialize(value);
}

public class Program
{
    public static void Main()
    {
        var people = new List<Person>();
        var json = JsonHelper.ToJson(people);  // List<Person> 전달
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

        // List<Person>과 Person이 모두 수집되어야 함
        Assert.Contains("Person", code);
        Assert.Contains("List", code);
    }

    [Fact]
    public void Generator_CrossAssembly_AutoDetectsTypeFromExternalMethod()
    {
        // Arrange - 외부 어셈블리에서 FastJson을 참조하는 제네릭 메서드 호출 시 타입 자동 등록 테스트
        // 1. 라이브러리 컴파일 생성 (FastJson 참조)
        var librarySource = @"
using FastJson;

namespace MyLibrary
{
    public static class JsonHelper
    {
        public static string CreateJson<T>(T value) => FastJson.FastJson.Serialize(value);
    }
}
";
        var libraryCompilation = CreateLibraryCompilation("MyLibrary", librarySource);

        // 라이브러리 컴파일 에러 확인
        var libraryDiags = libraryCompilation.GetDiagnostics();
        var libraryErrors = libraryDiags.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Empty(libraryErrors);  // 라이브러리 컴파일 성공 확인

        // 2. 라이브러리를 메모리에 emit하여 실제 어셈블리로 변환
        using var ms = new System.IO.MemoryStream();
        var emitResult = libraryCompilation.Emit(ms);
        Assert.True(emitResult.Success, string.Join("\n", emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        ms.Seek(0, System.IO.SeekOrigin.Begin);
        var libraryReference = MetadataReference.CreateFromStream(ms);

        // 3. 소비자 프로젝트 소스 (라이브러리 메서드 호출)
        var consumerSource = @"
using MyLibrary;

public class TestDto
{
    public string Name { get; set; }
    public int Value { get; set; }
}

public class Program
{
    public static void Main()
    {
        var dto = new TestDto { Name = ""Test"", Value = 123 };
        var json = JsonHelper.CreateJson(dto);  // 타입 추론으로 외부 메서드 호출
    }
}
";

        // Act - 라이브러리 참조를 포함한 generator 실행
        var (diagnostics, outputCompilation) = RunGeneratorWithExternalReference(consumerSource, libraryReference);

        // Assert
        var contextTree = outputCompilation.SyntaxTrees
            .FirstOrDefault(t => t.FilePath.Contains("FastJsonContext"));

        Assert.NotNull(contextTree);
        var code = contextTree.ToString();

        // TestDto가 자동으로 수집되어야 함
        Assert.Contains("TestDto", code);
        Assert.Contains("TestDtoSerializer", code);
        Assert.Contains("TestDtoDeserializer", code);
    }

    [Fact]
    public void Generator_CrossAssembly_AutoDetectsNestedGenericFromExternalMethod()
    {
        // Arrange - 외부 어셈블리 메서드에 List<T> 같은 중첩 제네릭 전달 시 테스트
        var librarySource = @"
using FastJson;

namespace MyLibrary
{
    public static class JsonHelper
    {
        public static string CreateJson<T>(T value) => FastJson.FastJson.Serialize(value);
    }
}
";
        var libraryCompilation = CreateLibraryCompilation("MyLibrary", librarySource);

        // 라이브러리를 메모리에 emit하여 실제 어셈블리로 변환
        using var ms = new System.IO.MemoryStream();
        var emitResult = libraryCompilation.Emit(ms);
        Assert.True(emitResult.Success);
        ms.Seek(0, System.IO.SeekOrigin.Begin);
        var libraryReference = MetadataReference.CreateFromStream(ms);

        var consumerSource = @"
using MyLibrary;
using System.Collections.Generic;

public class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
}

public class Program
{
    public static void Main()
    {
        var people = new List<Person>
        {
            new Person { Name = ""Alice"", Age = 30 }
        };
        var json = JsonHelper.CreateJson(people);  // 타입 추론으로 List<Person> 전달
    }
}
";

        // Act
        var (diagnostics, outputCompilation) = RunGeneratorWithExternalReference(consumerSource, libraryReference);

        // Assert
        var contextTree = outputCompilation.SyntaxTrees
            .FirstOrDefault(t => t.FilePath.Contains("FastJsonContext"));

        Assert.NotNull(contextTree);
        var code = contextTree.ToString();

        // List<Person>과 Person 둘 다 수집되어야 함
        Assert.Contains("Person", code);
        Assert.Contains("List", code);
    }

    [Fact]
    public void Generator_CrossAssembly_NonFastJsonLibrary_DoesNotRegister()
    {
        // Arrange - FastJson을 참조하지 않는 외부 라이브러리는 타입 등록하지 않음
        var librarySource = @"
namespace OtherLibrary
{
    public static class SomeHelper
    {
        public static string Process<T>(T value) => value?.ToString() ?? """";
    }
}
";
        // FastJson 참조 없이 라이브러리 컴파일
        var libraryCompilation = CreateLibraryCompilationWithoutFastJson("OtherLibrary", librarySource);
        var libraryReference = libraryCompilation.ToMetadataReference();

        var consumerSource = @"
using OtherLibrary;

public class MyData
{
    public string Info { get; set; }
}

public class Program
{
    public static void Main()
    {
        var data = new MyData { Info = ""test"" };
        var result = SomeHelper.Process(data);  // FastJson과 무관한 메서드
    }
}
";

        // Act
        var (diagnostics, outputCompilation) = RunGeneratorWithExternalReference(consumerSource, libraryReference);

        // Assert - 생성된 코드가 없거나 MyData가 없어야 함
        var contextTree = outputCompilation.SyntaxTrees
            .FirstOrDefault(t => t.FilePath.Contains("FastJsonContext"));

        if (contextTree != null)
        {
            var code = contextTree.ToString();
            Assert.DoesNotContain("MyData", code);
        }
        // 또는 컨텍스트 자체가 생성되지 않음
    }

    private static Compilation CreateLibraryCompilation(string assemblyName, string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Text.Json.JsonSerializer).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(global::FastJson.FastJsonOptionsAttribute).Assembly.Location)
        };

        var assemblyPath = System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        references.Add(MetadataReference.CreateFromFile(System.IO.Path.Combine(assemblyPath, "System.Runtime.dll")));
        references.Add(MetadataReference.CreateFromFile(System.IO.Path.Combine(assemblyPath, "System.Collections.dll")));
        references.Add(MetadataReference.CreateFromFile(System.IO.Path.Combine(assemblyPath, "System.Memory.dll")));
        references.Add(MetadataReference.CreateFromFile(typeof(System.Buffers.IBufferWriter<>).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(System.Text.Encodings.Web.JavaScriptEncoder).Assembly.Location));

        return CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static Compilation CreateLibraryCompilationWithoutFastJson(string assemblyName, string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location)
        };

        var assemblyPath = System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        references.Add(MetadataReference.CreateFromFile(System.IO.Path.Combine(assemblyPath, "System.Runtime.dll")));

        return CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static (ImmutableArray<Diagnostic>, Compilation) RunGeneratorWithExternalReference(
        string source, MetadataReference externalReference)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Text.Json.JsonSerializer).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(global::FastJson.FastJsonOptionsAttribute).Assembly.Location),
            externalReference  // 외부 라이브러리 참조 추가
        };

        var assemblyPath = System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        references.Add(MetadataReference.CreateFromFile(System.IO.Path.Combine(assemblyPath, "System.Runtime.dll")));
        references.Add(MetadataReference.CreateFromFile(System.IO.Path.Combine(assemblyPath, "System.Collections.dll")));
        references.Add(MetadataReference.CreateFromFile(System.IO.Path.Combine(assemblyPath, "System.Memory.dll")));
        references.Add(MetadataReference.CreateFromFile(typeof(System.Buffers.IBufferWriter<>).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(System.Text.Encodings.Web.JavaScriptEncoder).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "ConsumerAssembly",
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
