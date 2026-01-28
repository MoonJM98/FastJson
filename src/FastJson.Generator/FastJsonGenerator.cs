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
/// 3. Recursively collecting all nested types (properties, generic arguments, etc.)
/// 4. Generating JsonTypeInfo for each type using System.Text.Json metadata APIs
/// 5. Generating a module initializer that configures FastJson at startup
/// </para>
/// <para>
/// The generator is incremental, meaning it only regenerates code when the
/// relevant source code changes, providing fast rebuild times.
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
        // Step 1: Collect types from FastJson.Serialize<T>/Deserialize<T> invocations
        var invocationTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsFastJsonInvocation(node),
                transform: static (ctx, ct) => GetTypeFromInvocation(ctx, ct))
            .Where(static t => t is not null)
            .Select(static (t, _) => t!);

        // Step 2: Collect types from [assembly: FastJsonInclude(typeof(T))] attributes
        var includeAttributeTypes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: "FastJson.FastJsonIncludeAttribute",
                predicate: static (node, _) => true,
                transform: static (ctx, ct) => GetTypeFromIncludeAttribute(ctx, ct))
            .Where(static t => t is not null)
            .Select(static (t, _) => t!);

        // Step 3: Collect options from [assembly: FastJsonOptions(...)] attribute
        var optionsProvider = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: "FastJson.FastJsonOptionsAttribute",
                predicate: static (node, _) => true,
                transform: static (ctx, ct) => GetOptionsFromAttribute(ctx, ct))
            .Collect()
            .Select(static (options, _) => options.IsEmpty ? FastJsonOptionsModel.Default : options[0]);

        // Step 4: Combine all directly referenced types
        var allDirectTypes = invocationTypes.Collect()
            .Combine(includeAttributeTypes.Collect())
            .Select(static (pair, _) =>
            {
                var combined = new List<ITypeSymbol>();
                combined.AddRange(pair.Left);
                combined.AddRange(pair.Right);
                return combined;
            });

        // Step 5: Expand types using TypeCollector (collect nested types, generic arguments, etc.)
        var allTypesResult = allDirectTypes
            .Select(static (types, _) => TypeCollector.CollectAllTypesWithDiagnostics(types));

        // Step 6: Combine types with options
        var combined = allTypesResult.Combine(optionsProvider);

        // Step 7: Register source output - generate the serialization context
        context.RegisterSourceOutput(combined, static (spc, source) =>
        {
            var (result, options) = source;

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
            var contextCode = CodeEmitter.EmitContext(result.Types, options);
            spc.AddSource("FastJsonContext.g.cs", contextCode);
        });
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
            if (memberName is "Serialize" or "Deserialize" or "SerializeAsync" or "DeserializeAsync")
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

        // Skip open generic type parameters - these trigger FJ001 analyzer warning
        if (typeArg.TypeKind == TypeKind.TypeParameter)
            return null;

        return typeArg;
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
