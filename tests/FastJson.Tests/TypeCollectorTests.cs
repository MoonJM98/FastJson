using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using FastJson.Generator;

namespace FastJson.Tests;

public class TypeCollectorTests
{
    [Fact]
    public void CollectAllTypes_SimpleClass_CollectsType()
    {
        // Arrange
        var compilation = CreateCompilation(@"
            public class Person
            {
                public string Name { get; set; }
                public int Age { get; set; }
            }
        ");

        var personType = compilation.GetTypeByMetadataName("Person")!;

        // Act
        var types = TypeCollector.CollectAllTypes(new[] { personType });

        // Assert
        Assert.Single(types);
        Assert.Contains(types, t => t.TypeName == "Person");
    }

    [Fact]
    public void CollectAllTypes_NestedTypes_CollectsAll()
    {
        // Arrange
        var compilation = CreateCompilation(@"
            public class Order
            {
                public int Id { get; set; }
                public Customer Customer { get; set; }
            }
            public class Customer
            {
                public string Name { get; set; }
            }
        ");

        var orderType = compilation.GetTypeByMetadataName("Order")!;

        // Act
        var types = TypeCollector.CollectAllTypes(new[] { orderType });

        // Assert
        Assert.Equal(2, types.Length);
        Assert.Contains(types, t => t.TypeName == "Order");
        Assert.Contains(types, t => t.TypeName == "Customer");
    }

    [Fact]
    public void CollectAllTypes_GenericList_CollectsElementType()
    {
        // Arrange
        var compilation = CreateCompilation(@"
            using System.Collections.Generic;
            public class Container
            {
                public List<Item> Items { get; set; }
            }
            public class Item
            {
                public string Name { get; set; }
            }
        ");

        var containerType = compilation.GetTypeByMetadataName("Container")!;

        // Act
        var types = TypeCollector.CollectAllTypes(new[] { containerType });

        // Assert
        Assert.Contains(types, t => t.TypeName == "Container");
        Assert.Contains(types, t => t.TypeName == "Item");
        Assert.Contains(types, t => t.TypeName.Contains("List"));
    }

    [Fact]
    public void CollectAllTypes_PrimitiveProperty_SkipsPrimitive()
    {
        // Arrange
        var compilation = CreateCompilation(@"
            public class Simple
            {
                public int Number { get; set; }
                public string Text { get; set; }
                public bool Flag { get; set; }
            }
        ");

        var simpleType = compilation.GetTypeByMetadataName("Simple")!;

        // Act
        var types = TypeCollector.CollectAllTypes(new[] { simpleType });

        // Assert
        Assert.Single(types);
        Assert.Equal("Simple", types[0].TypeName);
    }

    [Fact]
    public void CollectAllTypes_CircularReference_DoesNotLoop()
    {
        // Arrange
        var compilation = CreateCompilation(@"
            public class Node
            {
                public string Value { get; set; }
                public Node Next { get; set; }
            }
        ");

        var nodeType = compilation.GetTypeByMetadataName("Node")!;

        // Act
        var types = TypeCollector.CollectAllTypes(new[] { nodeType });

        // Assert
        Assert.Single(types);
        Assert.Equal("Node", types[0].TypeName);
    }

    [Fact]
    public void CreateTypeModel_GenericType_SetsIsGenericTrue()
    {
        // Arrange
        var compilation = CreateCompilation(@"
            using System.Collections.Generic;
            public class Test
            {
                public List<string> Items { get; set; }
            }
        ");

        var testType = compilation.GetTypeByMetadataName("Test")!;
        var listType = ((IPropertySymbol)testType.GetMembers("Items")[0]).Type;

        // Act
        var model = TypeCollector.CreateTypeModel(listType);

        // Assert
        Assert.True(model.IsGeneric);
        Assert.True(model.IsCollection);
    }

    [Fact]
    public void ContextPropertyName_HasPrefixAndHash()
    {
        // Arrange
        var compilation = CreateCompilation(@"
            public class MyClass { }
        ");

        var type = compilation.GetTypeByMetadataName("MyClass")!;

        // Act
        var model = TypeCollector.CreateTypeModel(type);

        // Assert
        Assert.StartsWith("__FJ_", model.ContextPropertyName);
        Assert.Matches(@"__FJ_MyClass_[A-F0-9]{6}", model.ContextPropertyName);
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location)
        };

        return CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
