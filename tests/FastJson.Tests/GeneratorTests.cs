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

        Assert.Contains("FastJsonTypeInfoResolver", contextCode);
        Assert.Contains("Person", contextCode);
        Assert.Contains("FastJsonCache", contextCode);
        Assert.Contains("MarkInitialized", contextCode);
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
