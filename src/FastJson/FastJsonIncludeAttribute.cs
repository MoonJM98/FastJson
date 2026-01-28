using System;

namespace FastJson;

/// <summary>
/// Explicitly includes a type for JSON serialization.
/// Use this for external types from other assemblies that are not automatically detected.
/// Apply this attribute at the assembly level.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class FastJsonIncludeAttribute : Attribute
{
    /// <summary>
    /// Gets the type to include for JSON serialization.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FastJsonIncludeAttribute"/> class.
    /// </summary>
    /// <param name="type">The type to include for JSON serialization.</param>
    public FastJsonIncludeAttribute(Type type)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
    }
}
