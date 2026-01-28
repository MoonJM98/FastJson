using Microsoft.CodeAnalysis;

namespace FastJson.Generator;

/// <summary>
/// Diagnostic descriptors for FastJson analyzer.
/// </summary>
public static class DiagnosticDescriptors
{
    private const string Category = "FastJson";

    /// <summary>
    /// FJ001: Generic type parameter not allowed.
    /// FastJson.Serialize&lt;T&gt;() and FastJson.Deserialize&lt;T&gt;() require concrete types.
    /// </summary>
    public static readonly DiagnosticDescriptor GenericTypeParameterNotAllowed = new(
        id: "FJ001",
        title: "Generic type parameter not allowed",
        messageFormat: "FastJson cannot use type parameter '{0}'. Use a concrete type instead",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "FastJson.Serialize<T> and FastJson.Deserialize<T> require concrete types at compile time for AOT source generation. Type parameters cannot be resolved at compile time.");

    /// <summary>
    /// FJ002: Unsupported type.
    /// Types like object, dynamic, anonymous types are not supported.
    /// </summary>
    public static readonly DiagnosticDescriptor UnsupportedType = new(
        id: "FJ002",
        title: "Unsupported type",
        messageFormat: "Type '{0}' is not supported by FastJson: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "FastJson does not support certain types like object, dynamic, or anonymous types because they cannot be properly serialized at compile time.");

    /// <summary>
    /// FJ003: Type depth exceeded.
    /// Recursive type depth exceeds the maximum allowed (20 levels).
    /// </summary>
    public static readonly DiagnosticDescriptor TypeDepthExceeded = new(
        id: "FJ003",
        title: "Type depth exceeded",
        messageFormat: "Type '{0}' exceeds maximum nesting depth of {1} levels. Consider simplifying the type hierarchy",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The type hierarchy is too deep. This may indicate a circular reference or overly complex type structure.");

    /// <summary>
    /// FJ004: Type count exceeded.
    /// Total number of collected types exceeds the maximum allowed (500 types).
    /// </summary>
    public static readonly DiagnosticDescriptor TypeCountExceeded = new(
        id: "FJ004",
        title: "Type count exceeded",
        messageFormat: "Total type count ({0}) exceeds maximum of {1} types. Consider splitting serialization contexts",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Too many types are being serialized. Consider splitting your serialization into smaller contexts.");

    /// <summary>
    /// FJ005: Circular reference detected.
    /// Type A references Type B which references Type A.
    /// </summary>
    public static readonly DiagnosticDescriptor CircularReferenceDetected = new(
        id: "FJ005",
        title: "Circular reference detected",
        messageFormat: "Circular reference detected: {0}. Use [JsonIgnore] to break the cycle if needed",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A circular reference was detected in the type hierarchy. FastJson handles this but serialization may result in issues if not properly handled with [JsonIgnore].");

    /// <summary>
    /// FJ006: External type usage.
    /// Type from external assembly (NuGet package) is being serialized.
    /// </summary>
    public static readonly DiagnosticDescriptor ExternalTypeUsage = new(
        id: "FJ006",
        title: "External type usage",
        messageFormat: "Type '{0}' is from external assembly '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Types from external assemblies are being serialized. This is informational only.");
}
