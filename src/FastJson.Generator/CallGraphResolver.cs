using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastJson.Generator;

/// <summary>
/// Resolves concrete types for generic type parameters by tracking call sites.
/// Builds a call graph to trace type arguments from invocation points back to concrete types.
/// </summary>
/// <remarks>
/// <para>
/// When FastJson.Deserialize&lt;T&gt;() is used inside a generic method like Process&lt;T&gt;(),
/// the type T is an open type parameter at that location. This resolver traces back to find
/// where Process&lt;T&gt;() is called with concrete types like Process&lt;User&gt;().
/// </para>
/// <para>
/// The call graph is built once per compilation and cached for efficient lookups.
/// This enables automatic type registration without requiring manual [FastJsonInclude] attributes.
/// </para>
/// </remarks>
public class CallGraphResolver
{
    private readonly Dictionary<IMethodSymbol, List<CallSiteInfo>> _callGraph;
    private readonly Compilation _compilation;

    /// <summary>
    /// Information about a call site where a generic method is invoked.
    /// </summary>
    public class CallSiteInfo
    {
        /// <summary>
        /// The invocation syntax node.
        /// </summary>
        public InvocationExpressionSyntax Invocation { get; init; } = null!;

        /// <summary>
        /// The concrete type arguments used at this call site.
        /// </summary>
        public ITypeSymbol[] TypeArguments { get; init; } = null!;

        /// <summary>
        /// The method containing this call site (may itself be generic).
        /// </summary>
        public IMethodSymbol? ContainingMethod { get; init; }
    }

    private CallGraphResolver(Compilation compilation, Dictionary<IMethodSymbol, List<CallSiteInfo>> callGraph)
    {
        _compilation = compilation;
        _callGraph = callGraph;
    }

    /// <summary>
    /// Builds a call graph from the given compilation.
    /// </summary>
    public static CallGraphResolver Build(Compilation compilation)
    {
        var callGraph = new Dictionary<IMethodSymbol, List<CallSiteInfo>>(SymbolEqualityComparer.Default);

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            // Find all method invocations
            var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

            foreach (var invocation in invocations)
            {
                if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol calledMethod)
                    continue;

                // We're interested in generic method calls
                if (!calledMethod.IsGenericMethod)
                    continue;

                // Get the original definition (before type substitution)
                var originalMethod = calledMethod.OriginalDefinition;

                // Get the containing method of this invocation
                var containingMethodSyntax = invocation.Ancestors()
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault();

                IMethodSymbol? containingMethod = null;
                if (containingMethodSyntax != null)
                {
                    containingMethod = semanticModel.GetDeclaredSymbol(containingMethodSyntax);
                }

                // Record the call site
                var callSite = new CallSiteInfo
                {
                    Invocation = invocation,
                    TypeArguments = calledMethod.TypeArguments.ToArray(),
                    ContainingMethod = containingMethod
                };

                if (!callGraph.TryGetValue(originalMethod, out var callSites))
                {
                    callSites = new List<CallSiteInfo>();
                    callGraph[originalMethod] = callSites;
                }

                callSites.Add(callSite);
            }
        }

        return new CallGraphResolver(compilation, callGraph);
    }

    /// <summary>
    /// Resolves concrete types for a type parameter used in FastJson calls.
    /// </summary>
    /// <param name="typeParameter">The open type parameter (e.g., T).</param>
    /// <param name="containingMethod">The generic method containing the FastJson call.</param>
    /// <param name="maxDepth">Maximum depth for recursive resolution (default: 5).</param>
    /// <returns>All concrete types that the type parameter can resolve to.</returns>
    public IEnumerable<ITypeSymbol> ResolveTypeParameter(
        ITypeParameterSymbol typeParameter,
        IMethodSymbol containingMethod,
        int maxDepth = 5)
    {
        var resolvedTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var visited = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

        ResolveTypeParameterRecursive(typeParameter, containingMethod, resolvedTypes, visited, maxDepth);

        return resolvedTypes;
    }

    private void ResolveTypeParameterRecursive(
        ITypeParameterSymbol typeParameter,
        IMethodSymbol containingMethod,
        HashSet<ITypeSymbol> resolvedTypes,
        HashSet<IMethodSymbol> visited,
        int remainingDepth)
    {
        if (remainingDepth <= 0)
            return;

        if (!visited.Add(containingMethod))
            return;

        // Get the original definition to look up in call graph
        var originalMethod = containingMethod.OriginalDefinition;

        if (!_callGraph.TryGetValue(originalMethod, out var callSites))
            return;

        // Find the index of this type parameter
        var typeParamIndex = -1;
        for (int i = 0; i < originalMethod.TypeParameters.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(originalMethod.TypeParameters[i], typeParameter) ||
                originalMethod.TypeParameters[i].Name == typeParameter.Name)
            {
                typeParamIndex = i;
                break;
            }
        }

        if (typeParamIndex < 0)
            return;

        foreach (var callSite in callSites)
        {
            if (typeParamIndex >= callSite.TypeArguments.Length)
                continue;

            var resolvedType = callSite.TypeArguments[typeParamIndex];

            // If the resolved type is itself a type parameter, recurse
            if (resolvedType is ITypeParameterSymbol nestedTypeParam &&
                callSite.ContainingMethod != null &&
                callSite.ContainingMethod.IsGenericMethod)
            {
                ResolveTypeParameterRecursive(
                    nestedTypeParam,
                    callSite.ContainingMethod,
                    resolvedTypes,
                    visited,
                    remainingDepth - 1);
            }
            else if (resolvedType.TypeKind != TypeKind.TypeParameter)
            {
                // Concrete type found
                resolvedTypes.Add(resolvedType);
            }
        }
    }

    /// <summary>
    /// Gets all call sites for a given method.
    /// </summary>
    public IReadOnlyList<CallSiteInfo> GetCallSites(IMethodSymbol method)
    {
        var originalMethod = method.OriginalDefinition;
        return _callGraph.TryGetValue(originalMethod, out var callSites)
            ? callSites
            : Array.Empty<CallSiteInfo>();
    }
}
