using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;
using FastJson.Generator;

namespace FastJson.Tests;

public class AnalyzerTests
{
    [Fact]
    public async Task Analyzer_ClassLevelGenericTypeParameter_NoFJ001_WhenTrackable()
    {
        // Arrange - 클래스 레벨 제네릭은 new Service<Person>()으로 추적 가능하므로 FJ001 발생 안함
        var source = @"
using FastJson;

public class Service<T>
{
    public string Process(T item)
    {
        return FastJson.FastJson.Serialize(item);
    }
}
";

        // Act
        var diagnostics = await RunAnalyzer(source);

        // Assert - No FJ001 because class-level generics are trackable via instantiation
        Assert.DoesNotContain(diagnostics, d => d.Id == "FJ001");
    }

    [Fact]
    public async Task Analyzer_ConcreteType_NoDiagnostic()
    {
        // Arrange
        var source = @"
using FastJson;

public class Person { public string Name { get; set; } }

public class Service
{
    public string Process()
    {
        return FastJson.FastJson.Serialize(new Person());
    }
}
";

        // Act
        var diagnostics = await RunAnalyzer(source);

        // Assert - no errors or warnings related to FastJson
        Assert.Empty(diagnostics.Where(d => d.Id.StartsWith("FJ") && d.Severity >= DiagnosticSeverity.Warning));
    }

    [Fact]
    public async Task Analyzer_DeserializeWithTypeParameter_NoFJ001_WhenTrackable()
    {
        // Arrange - 클래스 레벨 제네릭은 추적 가능
        var source = @"
using FastJson;

public class Service<T>
{
    public T? Process(string json)
    {
        return FastJson.FastJson.Deserialize<T>(json);
    }
}
";

        // Act
        var diagnostics = await RunAnalyzer(source);

        // Assert - No FJ001 because class-level generics are trackable
        Assert.DoesNotContain(diagnostics, d => d.Id == "FJ001");
    }

    [Fact]
    public async Task Analyzer_AsyncMethodWithTypeParameter_NoFJ001_WhenTrackable()
    {
        // Arrange - 클래스 레벨 제네릭은 추적 가능
        var source = @"
using FastJson;
using System.IO;
using System.Threading.Tasks;

public class Service<T>
{
    public async Task WriteAsync(Stream stream, T item)
    {
        await FastJson.FastJson.SerializeAsync(stream, item);
    }
}
";

        // Act
        var diagnostics = await RunAnalyzer(source);

        // Assert - No FJ001 because class-level generics are trackable
        Assert.DoesNotContain(diagnostics, d => d.Id == "FJ001");
    }

    [Fact]
    public async Task Analyzer_MethodLevelGenericParameter_NoFJ001_WhenTrackable()
    {
        // Arrange - 메서드 레벨 제네릭 T는 Process<Person>()으로 추적 가능
        var source = @"
using FastJson;

public class Service
{
    public string Process<T>(T item)
    {
        return FastJson.FastJson.Serialize(item);
    }
}
";

        // Act
        var diagnostics = await RunAnalyzer(source);

        // Assert - No FJ001 because method-level generics are trackable via invocation
        Assert.DoesNotContain(diagnostics, d => d.Id == "FJ001");
    }

    [Fact]
    public async Task Analyzer_ListOfGenericParameter_NoFJ001_WhenTrackable()
    {
        // Arrange - List<T>로 감싼 제네릭도 ProcessList<Person>()으로 추적 가능
        var source = @"
using FastJson;
using System.Collections.Generic;

public class Service
{
    public string ProcessList<T>(List<T> items)
    {
        return FastJson.FastJson.Serialize(items);
    }
}
";

        // Act
        var diagnostics = await RunAnalyzer(source);

        // Assert - No FJ001 because method-level generics are trackable
        Assert.DoesNotContain(diagnostics, d => d.Id == "FJ001");
    }

    [Fact]
    public async Task Analyzer_DictionaryWithGenericValue_NoFJ001_WhenTrackable()
    {
        // Arrange - Dictionary<string, T>도 클래스 레벨 제네릭이므로 추적 가능
        var source = @"
using FastJson;
using System.Collections.Generic;

public class Service<T>
{
    public string ProcessDict(Dictionary<string, T> items)
    {
        return FastJson.FastJson.Serialize(items);
    }
}
";

        // Act
        var diagnostics = await RunAnalyzer(source);

        // Assert - No FJ001 because class-level generics are trackable
        Assert.DoesNotContain(diagnostics, d => d.Id == "FJ001");
    }

    [Fact]
    public async Task Analyzer_NestedGenericWrapper_NoFJ001_WhenTrackable()
    {
        // Arrange - List<List<T>> 중첩 제네릭도 메서드 레벨이므로 추적 가능
        var source = @"
using FastJson;
using System.Collections.Generic;

public class Service
{
    public string Process<T>(List<List<T>> items)
    {
        return FastJson.FastJson.Serialize(items);
    }
}
";

        // Act
        var diagnostics = await RunAnalyzer(source);

        // Assert - No FJ001 because method-level generics are trackable
        Assert.DoesNotContain(diagnostics, d => d.Id == "FJ001");
    }

    [Fact]
    public async Task Analyzer_GenericDeserializeWithWrapper_NoFJ001_WhenTrackable()
    {
        // Arrange - Deserialize<List<T>>도 메서드 레벨이므로 추적 가능
        var source = @"
using FastJson;
using System.Collections.Generic;

public class Service
{
    public List<T>? Process<T>(string json)
    {
        return FastJson.FastJson.Deserialize<List<T>>(json);
    }
}
";

        // Act
        var diagnostics = await RunAnalyzer(source);

        // Assert - No FJ001 because method-level generics are trackable
        Assert.DoesNotContain(diagnostics, d => d.Id == "FJ001");
    }

    [Fact]
    public async Task Analyzer_ObjectType_ReportsFJ002()
    {
        // Arrange
        var source = @"
using FastJson;

public class Service
{
    public string Process(object item)
    {
        return FastJson.FastJson.Serialize<object>(item);
    }
}
";

        // Act
        var diagnostics = await RunAnalyzer(source);

        // Assert
        var fj002 = diagnostics.Where(d => d.Id == "FJ002").ToList();
        Assert.Single(fj002);
        Assert.Contains("System.Object", fj002[0].GetMessage());
    }

    [Fact]
    public async Task Analyzer_DelegateType_ReportsFJ002()
    {
        // Arrange
        var source = @"
using FastJson;
using System;

public class Service
{
    public string Process()
    {
        return FastJson.FastJson.Serialize<Action>(null);
    }
}
";

        // Act
        var diagnostics = await RunAnalyzer(source);

        // Assert
        var fj002 = diagnostics.Where(d => d.Id == "FJ002").ToList();
        Assert.Single(fj002);
        Assert.Contains("Delegate", fj002[0].GetMessage());
    }

    [Fact]
    public async Task Analyzer_SelfReferencingType_ReportsFJ005()
    {
        // Arrange
        var source = @"
using FastJson;

public class TreeNode
{
    public string Name { get; set; }
    public TreeNode Parent { get; set; }
}

public class Service
{
    public string Process()
    {
        return FastJson.FastJson.Serialize(new TreeNode());
    }
}
";

        // Act
        var diagnostics = await RunAnalyzer(source);

        // Assert
        var fj005 = diagnostics.Where(d => d.Id == "FJ005").ToList();
        Assert.Single(fj005);
        Assert.Contains("TreeNode", fj005[0].GetMessage());
    }

    [Fact]
    public async Task Analyzer_MutualReferenceTypes_ReportsFJ005()
    {
        // Arrange
        var source = @"
using FastJson;

public class Parent
{
    public string Name { get; set; }
    public Child Child { get; set; }
}

public class Child
{
    public string Name { get; set; }
    public Parent Parent { get; set; }
}

public class Service
{
    public string Process()
    {
        return FastJson.FastJson.Serialize(new Parent());
    }
}
";

        // Act
        var diagnostics = await RunAnalyzer(source);

        // Assert
        var fj005 = diagnostics.Where(d => d.Id == "FJ005").ToList();
        Assert.Single(fj005);
    }

    [Fact]
    public async Task Analyzer_ValidListType_NoError()
    {
        // Arrange
        var source = @"
using FastJson;
using System.Collections.Generic;

public class Person { public string Name { get; set; } }

public class Service
{
    public string Process()
    {
        var list = new List<Person>();
        return FastJson.FastJson.Serialize(list);
    }
}
";

        // Act
        var diagnostics = await RunAnalyzer(source);

        // Assert - no errors
        Assert.Empty(diagnostics.Where(d => d.Id.StartsWith("FJ") && d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public async Task Analyzer_PointerType_ReportsFJ002()
    {
        // Arrange
        var source = @"
using FastJson;

public unsafe class Service
{
    public string Process()
    {
        int* ptr = null;
        return FastJson.FastJson.Serialize<nint>((nint)ptr);
    }
}
";

        // Act
        var diagnostics = await RunAnalyzer(source);

        // Assert
        var fj002 = diagnostics.Where(d => d.Id == "FJ002").ToList();
        Assert.Single(fj002);
    }

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzer(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.IO.Stream).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Text.Json.JsonSerializer).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(global::FastJson.FastJsonOptionsAttribute).Assembly.Location)
        };

        var assemblyPath = System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        references.Add(MetadataReference.CreateFromFile(System.IO.Path.Combine(assemblyPath, "System.Runtime.dll")));
        references.Add(MetadataReference.CreateFromFile(System.IO.Path.Combine(assemblyPath, "System.Threading.Tasks.dll")));
        references.Add(MetadataReference.CreateFromFile(System.IO.Path.Combine(assemblyPath, "System.Collections.dll")));

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

        var analyzer = new FastJsonAnalyzer();
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(analyzer);

        var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }
}
