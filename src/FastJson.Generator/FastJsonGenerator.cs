using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using FastJson.Generator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastJson.Generator;

/// <summary>
/// Incremental source generator for FastJson.
/// Automatically detects FastJson.Serialize/Deserialize calls and generates
/// AOT-compatible serialization code without requiring manual type registration.
/// </summary>
/// <remarks>
/// <para>
/// This generator works by:
/// 1. Scanning for FastJson.Serialize&lt;T&gt;() and FastJson.Deserialize&lt;T&gt;() invocations
/// 2. Collecting types from [assembly: FastJsonInclude(typeof(T))] attributes
/// 3. Tracking generic type parameters through call graphs to resolve concrete types
/// 4. Recursively collecting all nested types (properties, generic arguments, etc.)
/// 5. Generating JsonTypeInfo for each type using System.Text.Json metadata APIs
/// 6. Generating a module initializer that configures FastJson at startup
/// </para>
/// <para>
/// The generator is incremental, meaning it only regenerates code when the
/// relevant source code changes, providing fast rebuild times.
/// </para>
/// <para>
/// <b>Generic Type Parameter Resolution:</b>
/// When FastJson is used inside a generic method like Process&lt;T&gt;(), the generator
/// builds a call graph to trace where the method is called with concrete types.
/// For example, if Process&lt;User&gt;() is called somewhere, User will be automatically
/// registered for serialization.
/// </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public class FastJsonGenerator : IIncrementalGenerator
{
    /// <summary>
    /// Initializes the incremental generator pipeline.
    /// </summary>
    /// <param name="context">The generator initialization context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Step 1: Collect concrete types from FastJson.Serialize<T>/Deserialize<T> invocations
        var concreteInvocationTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsFastJsonInvocation(node),
                transform: static (ctx, ct) => GetTypeFromInvocation(ctx, ct))
            .Where(static t => t is not null)
            .Select(static (t, _) => t!);

        // Step 2: Collect invocations with type parameters for call graph resolution
        var typeParameterInvocations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsFastJsonInvocation(node),
                transform: static (ctx, ct) => GetTypeParameterInvocation(ctx, ct))
            .Where(static t => t is not null)
            .Select(static (t, _) => t!);

        // Step 2.5: Collect types from generic class instantiations that use FastJson internally
        // e.g., new Wrapper<List<Person>>() where Wrapper<T> calls FastJson.Deserialize<T>()
        var genericClassInstantiations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsGenericObjectCreation(node),
                transform: static (ctx, ct) => GetTypesFromGenericInstantiation(ctx, ct))
            .Where(static t => t is not null)
            .SelectMany(static (t, _) => t!);

        // Step 2.6: Collect types from generic method invocations that use FastJson internally
        // e.g., WriteJson<Person>(value) where WriteJson<T> calls FastJson.Serialize<T>()
        var genericMethodInvocations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsGenericMethodInvocation(node),
                transform: static (ctx, ct) => GetTypesFromGenericMethodInvocation(ctx, ct))
            .Where(static t => t is not null)
            .SelectMany(static (t, _) => t!);

        // Step 3: Collect types from [assembly: FastJsonInclude(typeof(T))] attributes
        var includeAttributeTypes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: "FastJson.FastJsonIncludeAttribute",
                predicate: static (node, _) => true,
                transform: static (ctx, ct) => GetTypeFromIncludeAttribute(ctx, ct))
            .Where(static t => t is not null)
            .Select(static (t, _) => t!);

        // Step 4: Collect options from [assembly: FastJsonOptions(...)] attribute
        var optionsProvider = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: "FastJson.FastJsonOptionsAttribute",
                predicate: static (node, _) => true,
                transform: static (ctx, ct) => GetOptionsFromAttribute(ctx, ct))
            .Collect()
            .Select(static (options, _) => options.IsEmpty ? FastJsonOptionsModel.Default : options[0]);

        // Step 5: Combine compilation with type parameter invocations for call graph resolution
        var compilationAndTypeParams = context.CompilationProvider
            .Combine(typeParameterInvocations.Collect());

        // Step 6: Resolve type parameters through call graph
        var resolvedTypes = compilationAndTypeParams
            .Select(static (pair, ct) =>
            {
                var (compilation, typeParamInvocations) = pair;
                var resolvedList = new List<ITypeSymbol>();

                if (typeParamInvocations.IsEmpty)
                    return resolvedList;

                // Build call graph once
                var resolver = CallGraphResolver.Build(compilation);

                foreach (var info in typeParamInvocations)
                {
                    var resolved = resolver.ResolveTypeParameter(
                        info.TypeParameter,
                        info.ContainingMethod);

                    resolvedList.AddRange(resolved);
                }

                return resolvedList;
            });

        // Step 7: Combine all directly referenced types
        var allDirectTypes = concreteInvocationTypes.Collect()
            .Combine(includeAttributeTypes.Collect())
            .Combine(resolvedTypes)
            .Combine(genericClassInstantiations.Collect())
            .Combine(genericMethodInvocations.Collect())
            .Select(static (quint, _) =>
            {
                var combined = new List<ITypeSymbol>();
                combined.AddRange(quint.Left.Left.Left.Left);
                combined.AddRange(quint.Left.Left.Left.Right);
                combined.AddRange(quint.Left.Left.Right);
                combined.AddRange(quint.Left.Right);
                combined.AddRange(quint.Right);
                return combined;
            });

        // Step 8: Expand types using TypeCollector (collect nested types, generic arguments, etc.)
        var allTypesResult = allDirectTypes
            .Select(static (types, _) => TypeCollector.CollectAllTypesWithDiagnostics(types));

        // Step 9: Combine types with options and assembly name
        var combined = allTypesResult
            .Combine(optionsProvider)
            .Combine(context.CompilationProvider.Select(static (c, _) => c.AssemblyName ?? "Unknown"));

        // Step 10: Register source output - generate the serialization context
        context.RegisterSourceOutput(combined, static (spc, source) =>
        {
            var ((result, options), assemblyName) = source;

            // Report diagnostic if type count limit was exceeded
            if (result.TypeCountExceeded)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.TypeCountExceeded,
                    Location.None,
                    result.ActualTypeCount,
                    Models.TypeCollectionResult.MaxTypeCount));
            }

            // Report diagnostic if type depth limit was exceeded
            if (result.DepthExceeded)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.TypeDepthExceeded,
                    Location.None,
                    result.DepthExceededTypeName,
                    Models.TypeCollectionResult.MaxDepth));
            }

            // Skip code generation if no types were collected
            if (result.Types.Length == 0)
                return;

            // Generate FastJsonContext.g.cs with all type info and module initializer
            var contextCode = CodeEmitter.EmitContext(result.Types, options, assemblyName);
            spc.AddSource("FastJsonContext.g.cs", contextCode);
        });
    }

    /// <summary>
    /// Information about a FastJson invocation with an open type parameter.
    /// </summary>
    private class TypeParameterInvocationInfo
    {
        public ITypeParameterSymbol TypeParameter { get; init; } = null!;
        public IMethodSymbol ContainingMethod { get; init; } = null!;
    }

    /// <summary>
    /// Determines whether a syntax node is a potential FastJson method invocation.
    /// This is a quick syntactic check used to filter nodes before semantic analysis.
    /// </summary>
    /// <param name="node">The syntax node to check.</param>
    /// <returns>True if the node might be a FastJson invocation; otherwise, false.</returns>
    private static bool IsFastJsonInvocation(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        // Check for FastJson.Serialize<T>(...) or FastJson.Deserialize<T>(...)
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var memberName = memberAccess.Name.Identifier.Text;
            if (memberName is "Serialize" or "Deserialize" or "SerializeAsync" or "DeserializeAsync" or "SerializeToUtf8Bytes")
            {
                // Check for simple FastJson.Method call
                if (memberAccess.Expression is IdentifierNameSyntax identifier &&
                    identifier.Identifier.Text == "FastJson")
                {
                    return true;
                }

                // Check for qualified FastJson.FastJson.Method or global::FastJson.FastJson.Method
                if (memberAccess.Expression is MemberAccessExpressionSyntax qualifiedAccess &&
                    qualifiedAccess.Name.Identifier.Text == "FastJson")
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts the type argument from a FastJson method invocation.
    /// Only returns concrete types (not type parameters).
    /// </summary>
    /// <param name="context">The generator syntax context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The type symbol from the invocation, or null if extraction fails.</returns>
    private static ITypeSymbol? GetTypeFromInvocation(GeneratorSyntaxContext context, CancellationToken ct)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Get the method symbol to verify it's a FastJson method
        if (context.SemanticModel.GetSymbolInfo(invocation, ct).Symbol is not IMethodSymbol methodSymbol)
            return null;

        // Verify the containing type is FastJson.FastJson
        var containingType = methodSymbol.ContainingType;
        if (containingType?.Name != "FastJson" || containingType.ContainingNamespace?.Name != "FastJson")
            return null;

        // Get the type argument (e.g., T in Serialize<T>)
        if (methodSymbol.TypeArguments.Length != 1)
            return null;

        var typeArg = methodSymbol.TypeArguments[0];

        // Skip open generic type parameters - these are handled by GetTypeParameterInvocation
        if (typeArg.TypeKind == TypeKind.TypeParameter)
            return null;

        return typeArg;
    }

    /// <summary>
    /// Extracts type parameter invocation info for call graph resolution.
    /// Only returns info when the type argument is an open type parameter.
    /// </summary>
    /// <param name="context">The generator syntax context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Type parameter info if the invocation uses a type parameter, null otherwise.</returns>
    private static TypeParameterInvocationInfo? GetTypeParameterInvocation(GeneratorSyntaxContext context, CancellationToken ct)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Get the method symbol to verify it's a FastJson method
        if (context.SemanticModel.GetSymbolInfo(invocation, ct).Symbol is not IMethodSymbol methodSymbol)
            return null;

        // Verify the containing type is FastJson.FastJson
        var containingType = methodSymbol.ContainingType;
        if (containingType?.Name != "FastJson" || containingType.ContainingNamespace?.Name != "FastJson")
            return null;

        // Get the type argument (e.g., T in Serialize<T>)
        if (methodSymbol.TypeArguments.Length != 1)
            return null;

        var typeArg = methodSymbol.TypeArguments[0];

        // Only handle open generic type parameters
        if (typeArg is not ITypeParameterSymbol typeParameter)
            return null;

        // Find the containing method
        var containingMethodSyntax = invocation.Ancestors()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault();

        if (containingMethodSyntax == null)
            return null;

        var containingMethod = context.SemanticModel.GetDeclaredSymbol(containingMethodSyntax, ct);
        if (containingMethod == null || !containingMethod.IsGenericMethod)
            return null;

        return new TypeParameterInvocationInfo
        {
            TypeParameter = typeParameter,
            ContainingMethod = containingMethod
        };
    }

    /// <summary>
    /// Extracts the type from a FastJsonInclude attribute.
    /// </summary>
    /// <param name="context">The generator attribute syntax context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The type symbol from the attribute, or null if extraction fails.</returns>
    private static ITypeSymbol? GetTypeFromIncludeAttribute(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        var attribute = context.Attributes.FirstOrDefault(a =>
            a.AttributeClass?.Name == "FastJsonIncludeAttribute");

        if (attribute is null)
            return null;

        // The attribute takes a single Type parameter
        if (attribute.ConstructorArguments.Length != 1)
            return null;

        var typeArg = attribute.ConstructorArguments[0];
        if (typeArg.Value is not ITypeSymbol typeSymbol)
            return null;

        return typeSymbol;
    }

    /// <summary>
    /// Determines whether a syntax node is a generic object creation expression.
    /// e.g., new Wrapper&lt;List&lt;Person&gt;&gt;()
    /// </summary>
    private static bool IsGenericObjectCreation(SyntaxNode node)
    {
        if (node is ObjectCreationExpressionSyntax creation)
        {
            return creation.Type is GenericNameSyntax;
        }

        // Handle implicit object creation: Wrapper<T> x = new(...)
        if (node is ImplicitObjectCreationExpressionSyntax)
        {
            return true; // Will check in transform
        }

        return false;
    }

    /// <summary>
    /// Extracts type arguments from generic class instantiation if the class uses FastJson internally.
    /// </summary>
    private static IEnumerable<ITypeSymbol>? GetTypesFromGenericInstantiation(GeneratorSyntaxContext context, CancellationToken ct)
    {
        ITypeSymbol? createdType = null;

        if (context.Node is ObjectCreationExpressionSyntax creation)
        {
            createdType = context.SemanticModel.GetTypeInfo(creation, ct).Type;
        }
        else if (context.Node is ImplicitObjectCreationExpressionSyntax implicitCreation)
        {
            createdType = context.SemanticModel.GetTypeInfo(implicitCreation, ct).Type;
        }

        if (createdType is not INamedTypeSymbol namedType)
            return null;

        if (!namedType.IsGenericType || namedType.TypeArguments.Length == 0)
            return null;

        // Check if this type or any of its members use FastJson
        if (!TypeUsesFastJson(namedType.OriginalDefinition, context.SemanticModel.Compilation, ct))
            return null;

        // Collect all type arguments recursively
        var types = new List<ITypeSymbol>();
        CollectTypeArguments(namedType, types);
        return types;
    }

    /// <summary>
    /// Recursively collects all type arguments from a generic type.
    /// </summary>
    private static void CollectTypeArguments(ITypeSymbol type, List<ITypeSymbol> collected)
    {
        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            foreach (var typeArg in namedType.TypeArguments)
            {
                // Skip type parameters
                if (typeArg.TypeKind == TypeKind.TypeParameter)
                    continue;

                collected.Add(typeArg);
                CollectTypeArguments(typeArg, collected);
            }
        }
        else if (type is IArrayTypeSymbol arrayType)
        {
            collected.Add(arrayType.ElementType);
            CollectTypeArguments(arrayType.ElementType, collected);
        }
    }

    /// <summary>
    /// Determines whether a syntax node is a generic method invocation (not FastJson itself).
    /// e.g., WriteJson&lt;Person&gt;(value)
    /// </summary>
    private static bool IsGenericMethodInvocation(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        // Check for Method<T>(...) pattern
        if (invocation.Expression is GenericNameSyntax)
            return true;

        // Check for obj.Method<T>(...) pattern
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name is GenericNameSyntax)
        {
            // Exclude FastJson.Serialize<T> - handled elsewhere
            var memberName = memberAccess.Name.Identifier.Text;
            if (memberName is "Serialize" or "Deserialize" or "SerializeAsync" or "DeserializeAsync" or "SerializeToUtf8Bytes")
            {
                return false;
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// Extracts type arguments from generic method invocation if the method uses FastJson internally.
    /// </summary>
    private static IEnumerable<ITypeSymbol>? GetTypesFromGenericMethodInvocation(GeneratorSyntaxContext context, CancellationToken ct)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Get the method symbol
        if (context.SemanticModel.GetSymbolInfo(invocation, ct).Symbol is not IMethodSymbol methodSymbol)
            return null;

        // Must be a generic method with type arguments
        if (!methodSymbol.IsGenericMethod || methodSymbol.TypeArguments.Length == 0)
            return null;

        // Skip if any type argument is still a type parameter
        if (methodSymbol.TypeArguments.Any(t => t.TypeKind == TypeKind.TypeParameter))
            return null;

        // Check if the method definition uses FastJson with its type parameter
        if (!MethodUsesFastJsonWithTypeParameter(methodSymbol.OriginalDefinition, context.SemanticModel.Compilation, ct))
            return null;

        // Collect all type arguments recursively
        var types = new List<ITypeSymbol>();
        foreach (var typeArg in methodSymbol.TypeArguments)
        {
            types.Add(typeArg);
            CollectTypeArguments(typeArg, types);
        }
        return types;
    }

    /// <summary>
    /// Checks if a method definition uses FastJson with its type parameter.
    /// </summary>
    private static bool MethodUsesFastJsonWithTypeParameter(IMethodSymbol methodDefinition, Compilation compilation, CancellationToken ct)
    {
        if (!methodDefinition.IsGenericMethod)
            return false;

        var typeParameters = methodDefinition.TypeParameters;
        if (typeParameters.Length == 0)
            return false;

        // Get syntax references for this method
        foreach (var syntaxRef in methodDefinition.DeclaringSyntaxReferences)
        {
            var syntax = syntaxRef.GetSyntax(ct);
            if (syntax is not MethodDeclarationSyntax methodDecl)
                continue;

            var semanticModel = compilation.GetSemanticModel(syntax.SyntaxTree);

            // Find all FastJson invocations in this method
            var invocations = methodDecl.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (var invocation in invocations)
            {
                if (IsFastJsonInvocation(invocation))
                {
                    // Verify it's actually a FastJson call with a type parameter
                    if (semanticModel.GetSymbolInfo(invocation, ct).Symbol is IMethodSymbol calledMethod)
                    {
                        var containingType = calledMethod.ContainingType;
                        if (containingType?.Name == "FastJson" && containingType.ContainingNamespace?.Name == "FastJson")
                        {
                            // Check if the type argument is one of the method's type parameters
                            if (calledMethod.TypeArguments.Length == 1)
                            {
                                var typeArg = calledMethod.TypeArguments[0];
                                if (typeArg is ITypeParameterSymbol typeParam &&
                                    typeParameters.Any(tp => tp.Name == typeParam.Name))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a type definition uses FastJson in its members.
    /// </summary>
    private static bool TypeUsesFastJson(INamedTypeSymbol typeDefinition, Compilation compilation, CancellationToken ct)
    {
        // Get syntax references for this type
        foreach (var syntaxRef in typeDefinition.DeclaringSyntaxReferences)
        {
            var syntax = syntaxRef.GetSyntax(ct);
            if (syntax is not TypeDeclarationSyntax typeDecl)
                continue;

            var semanticModel = compilation.GetSemanticModel(syntax.SyntaxTree);

            // Find all FastJson invocations in this type
            var invocations = typeDecl.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (var invocation in invocations)
            {
                if (IsFastJsonInvocation(invocation))
                {
                    // Verify it's actually a FastJson call
                    if (semanticModel.GetSymbolInfo(invocation, ct).Symbol is IMethodSymbol methodSymbol)
                    {
                        var containingType = methodSymbol.ContainingType;
                        if (containingType?.Name == "FastJson" && containingType.ContainingNamespace?.Name == "FastJson")
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts serialization options from a FastJsonOptions attribute.
    /// </summary>
    /// <param name="context">The generator attribute syntax context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The options model parsed from the attribute.</returns>
    private static FastJsonOptionsModel GetOptionsFromAttribute(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        var attribute = context.Attributes.FirstOrDefault(a =>
            a.AttributeClass?.Name == "FastJsonOptionsAttribute");

        if (attribute is null)
            return FastJsonOptionsModel.Default;

        // Parse all named arguments with defaults
        string propertyNamingPolicy = "CamelCase";
        bool writeIndented = false;
        bool ignoreReadOnlyProperties = false;
        bool defaultIgnoreCondition = false;
        bool propertyNameCaseInsensitive = false;
        bool allowTrailingCommas = false;
        bool readCommentHandling = false;

        foreach (var namedArg in attribute.NamedArguments)
        {
            switch (namedArg.Key)
            {
                case "PropertyNamingPolicy":
                    propertyNamingPolicy = namedArg.Value.Value as string ?? "CamelCase";
                    break;
                case "WriteIndented":
                    writeIndented = namedArg.Value.Value is true;
                    break;
                case "IgnoreReadOnlyProperties":
                    ignoreReadOnlyProperties = namedArg.Value.Value is true;
                    break;
                case "DefaultIgnoreCondition":
                    defaultIgnoreCondition = namedArg.Value.Value is true;
                    break;
                case "PropertyNameCaseInsensitive":
                    propertyNameCaseInsensitive = namedArg.Value.Value is true;
                    break;
                case "AllowTrailingCommas":
                    allowTrailingCommas = namedArg.Value.Value is true;
                    break;
                case "ReadCommentHandling":
                    readCommentHandling = namedArg.Value.Value is true;
                    break;
            }
        }

        return new FastJsonOptionsModel(
            propertyNamingPolicy,
            writeIndented,
            ignoreReadOnlyProperties,
            defaultIgnoreCondition,
            propertyNameCaseInsensitive,
            allowTrailingCommas,
            readCommentHandling);
    }
}
