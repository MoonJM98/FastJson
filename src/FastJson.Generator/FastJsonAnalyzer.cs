using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FastJson.Generator;

/// <summary>
/// Analyzer that detects invalid usage of FastJson methods.
/// Reports diagnostics for unsupported types and invalid usage patterns.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class FastJsonAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.GenericTypeParameterNotAllowed,
            DiagnosticDescriptors.UnsupportedType,
            DiagnosticDescriptors.CircularReferenceDetected,
            DiagnosticDescriptors.ExternalTypeUsage);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Check if it's a FastJson method call
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return;

        var memberName = memberAccess.Name.Identifier.Text;
        if (memberName is not ("Serialize" or "Deserialize" or "SerializeAsync" or "DeserializeAsync"))
            return;

        // Get the method symbol
        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol methodSymbol)
            return;

        // Check if it's the FastJson class
        var containingType = methodSymbol.ContainingType;
        if (containingType?.Name != "FastJson" || containingType.ContainingNamespace?.Name != "FastJson")
            return;

        // Check if the type argument is an open generic type parameter
        if (methodSymbol.TypeArguments.Length != 1)
            return;

        var typeArg = methodSymbol.TypeArguments[0];
        var location = memberAccess.Name.GetLocation();

        // FJ001: Generic type parameter not allowed
        if (typeArg.TypeKind == TypeKind.TypeParameter)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GenericTypeParameterNotAllowed,
                location,
                typeArg.Name));
            return;
        }

        // FJ002: Unsupported types
        var unsupportedReason = GetUnsupportedTypeReason(typeArg);
        if (unsupportedReason != null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.UnsupportedType,
                location,
                typeArg.ToDisplayString(),
                unsupportedReason));
            return;
        }

        // FJ005: Check for simple circular references (self-referencing types)
        var circularPath = DetectSimpleCircularReference(typeArg);
        if (circularPath != null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.CircularReferenceDetected,
                location,
                circularPath));
        }

        // FJ006: External type usage (info only)
        if (IsExternalType(typeArg, context.Compilation))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ExternalTypeUsage,
                location,
                typeArg.ToDisplayString(),
                typeArg.ContainingAssembly?.Name ?? "unknown"));
        }
    }

    /// <summary>
    /// Checks if a type is unsupported and returns the reason.
    /// </summary>
    private static string? GetUnsupportedTypeReason(ITypeSymbol type)
    {
        // object type
        if (type.SpecialType == SpecialType.System_Object)
        {
            return "System.Object cannot be serialized. Use a concrete type";
        }

        // dynamic type (represented as object with dynamic attribute)
        if (type.TypeKind == TypeKind.Dynamic)
        {
            return "dynamic type cannot be serialized. Use a concrete type";
        }

        // Anonymous types
        if (type.IsAnonymousType)
        {
            return "Anonymous types cannot be serialized. Define a named type";
        }

        // Error types
        if (type.TypeKind == TypeKind.Error)
        {
            return "Type could not be resolved";
        }

        // Pointer types
        if (type.TypeKind == TypeKind.Pointer)
        {
            return "Pointer types cannot be serialized";
        }

        // Function pointer types
        if (type.TypeKind == TypeKind.FunctionPointer)
        {
            return "Function pointer types cannot be serialized";
        }

        // Delegate types
        if (type.TypeKind == TypeKind.Delegate)
        {
            return "Delegate types cannot be serialized";
        }

        // Check for System.Type
        if (type.ToDisplayString() == "System.Type")
        {
            return "System.Type cannot be serialized";
        }

        // Check for IntPtr/UIntPtr (nint/nuint)
        if (type.SpecialType == SpecialType.System_IntPtr || type.SpecialType == SpecialType.System_UIntPtr)
        {
            return "IntPtr/UIntPtr (nint/nuint) cannot be serialized";
        }

        // Check for Span<T> and ReadOnlySpan<T>
        if (type is INamedTypeSymbol namedType)
        {
            var fullName = type.ToDisplayString();
            if (fullName.StartsWith("System.Span<") || fullName.StartsWith("System.ReadOnlySpan<"))
            {
                return "Span types cannot be serialized";
            }

            if (fullName.StartsWith("System.Memory<") || fullName.StartsWith("System.ReadOnlyMemory<"))
            {
                return "Memory types cannot be serialized";
            }

            // Check if it's an open generic type definition
            if (namedType.IsUnboundGenericType)
            {
                return "Unbound generic types cannot be serialized. Provide type arguments";
            }
        }

        return null;
    }

    /// <summary>
    /// Detects simple circular references (type references itself directly or through one level).
    /// </summary>
    private static string? DetectSimpleCircularReference(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
            return null;

        // Skip collections and primitives
        if (IsPrimitiveOrBuiltIn(type))
            return null;

        var typeName = type.ToDisplayString();
        var visited = new HashSet<string> { typeName };

        foreach (var member in type.GetMembers())
        {
            if (member is IPropertySymbol property &&
                property.DeclaredAccessibility == Accessibility.Public &&
                !property.IsStatic)
            {
                var propType = property.Type;

                // Unwrap nullable
                if (propType is INamedTypeSymbol nullable &&
                    nullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
                    nullable.TypeArguments.Length == 1)
                {
                    propType = nullable.TypeArguments[0];
                }

                // Unwrap collections
                propType = UnwrapCollectionElement(propType);
                if (propType == null)
                    continue;

                var propTypeName = propType.ToDisplayString();

                // Direct self-reference
                if (propTypeName == typeName)
                {
                    return $"{typeName} -> {property.Name} -> {typeName}";
                }

                // One-level indirect reference
                if (propType is INamedTypeSymbol propNamedType && !IsPrimitiveOrBuiltIn(propType))
                {
                    foreach (var innerMember in propType.GetMembers())
                    {
                        if (innerMember is IPropertySymbol innerProp &&
                            innerProp.DeclaredAccessibility == Accessibility.Public &&
                            !innerProp.IsStatic)
                        {
                            var innerPropType = UnwrapCollectionElement(innerProp.Type);
                            if (innerPropType != null && innerPropType.ToDisplayString() == typeName)
                            {
                                return $"{typeName} -> {property.Name} ({propTypeName}) -> {innerProp.Name} -> {typeName}";
                            }
                        }
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Unwraps collection types to get the element type.
    /// </summary>
    private static ITypeSymbol? UnwrapCollectionElement(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol arrayType)
        {
            return arrayType.ElementType;
        }

        if (type is INamedTypeSymbol namedType)
        {
            // Check for IEnumerable<T>
            var enumerableInterface = namedType.AllInterfaces
                .FirstOrDefault(i => i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T);

            if (enumerableInterface != null && enumerableInterface.TypeArguments.Length == 1)
            {
                // But not string (which implements IEnumerable<char>)
                if (type.SpecialType != SpecialType.System_String)
                {
                    return enumerableInterface.TypeArguments[0];
                }
            }
        }

        return type;
    }

    /// <summary>
    /// Checks if a type is primitive or built-in.
    /// </summary>
    private static bool IsPrimitiveOrBuiltIn(ITypeSymbol type)
    {
        if (type.SpecialType != SpecialType.None)
            return true;

        var fullName = type.ToDisplayString();
        return fullName switch
        {
            "System.DateTime" => true,
            "System.DateTimeOffset" => true,
            "System.TimeSpan" => true,
            "System.Guid" => true,
            "System.Uri" => true,
            "System.Version" => true,
            "System.DateOnly" => true,
            "System.TimeOnly" => true,
            _ => type.TypeKind == TypeKind.Enum
        };
    }

    /// <summary>
    /// Checks if a type is from an external assembly.
    /// </summary>
    private static bool IsExternalType(ITypeSymbol type, Compilation compilation)
    {
        // Skip primitive types
        if (IsPrimitiveOrBuiltIn(type))
            return false;

        // Skip collection element types
        if (type is IArrayTypeSymbol)
            return false;

        var assembly = type.ContainingAssembly;
        if (assembly == null)
            return false;

        // Check if the assembly is the main compilation
        if (SymbolEqualityComparer.Default.Equals(assembly, compilation.Assembly))
            return false;

        // Check if it's a well-known framework assembly
        var assemblyName = assembly.Name;
        if (assemblyName.StartsWith("System.") ||
            assemblyName == "System" ||
            assemblyName == "mscorlib" ||
            assemblyName == "netstandard" ||
            assemblyName.StartsWith("Microsoft."))
        {
            return false;
        }

        return true;
    }
}
