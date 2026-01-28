using System;

namespace FastJson.Generator.Models;

/// <summary>
/// Value-equatable model representing FastJson serialization options.
/// Parsed from the [assembly: FastJsonOptions(...)] attribute.
/// </summary>
/// <remarks>
/// This model is used by the source generator to configure the generated
/// JsonSerializerOptions. All properties have sensible defaults matching
/// common JSON conventions.
/// </remarks>
public readonly struct FastJsonOptionsModel : IEquatable<FastJsonOptionsModel>
{
    /// <summary>
    /// Gets the property naming policy.
    /// Supported values: "CamelCase" (default), "PascalCase", "SnakeCaseLower", "SnakeCaseUpper", "KebabCaseLower", "KebabCaseUpper".
    /// </summary>
    public string PropertyNamingPolicy { get; }

    /// <summary>
    /// Gets whether JSON output should be formatted with indentation for readability.
    /// Default is false (compact JSON).
    /// </summary>
    public bool WriteIndented { get; }

    /// <summary>
    /// Gets whether read-only properties should be ignored during serialization.
    /// Default is false.
    /// </summary>
    public bool IgnoreReadOnlyProperties { get; }

    /// <summary>
    /// Gets whether properties with default values should be ignored during serialization.
    /// Default is false.
    /// </summary>
    public bool DefaultIgnoreCondition { get; }

    /// <summary>
    /// Gets whether property name matching should be case-insensitive during deserialization.
    /// Default is false.
    /// </summary>
    public bool PropertyNameCaseInsensitive { get; }

    /// <summary>
    /// Gets whether trailing commas should be allowed in JSON input.
    /// Default is false.
    /// </summary>
    public bool AllowTrailingCommas { get; }

    /// <summary>
    /// Gets whether comments should be allowed in JSON input.
    /// Default is false.
    /// </summary>
    public bool ReadCommentHandling { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FastJsonOptionsModel"/> struct.
    /// </summary>
    /// <param name="propertyNamingPolicy">The property naming policy.</param>
    /// <param name="writeIndented">Whether to write indented JSON.</param>
    /// <param name="ignoreReadOnlyProperties">Whether to ignore read-only properties.</param>
    /// <param name="defaultIgnoreCondition">Whether to ignore properties with default values.</param>
    /// <param name="propertyNameCaseInsensitive">Whether property name matching is case-insensitive.</param>
    /// <param name="allowTrailingCommas">Whether to allow trailing commas.</param>
    /// <param name="readCommentHandling">Whether to allow comments in JSON.</param>
    public FastJsonOptionsModel(
        string propertyNamingPolicy = "CamelCase",
        bool writeIndented = false,
        bool ignoreReadOnlyProperties = false,
        bool defaultIgnoreCondition = false,
        bool propertyNameCaseInsensitive = false,
        bool allowTrailingCommas = false,
        bool readCommentHandling = false)
    {
        PropertyNamingPolicy = propertyNamingPolicy;
        WriteIndented = writeIndented;
        IgnoreReadOnlyProperties = ignoreReadOnlyProperties;
        DefaultIgnoreCondition = defaultIgnoreCondition;
        PropertyNameCaseInsensitive = propertyNameCaseInsensitive;
        AllowTrailingCommas = allowTrailingCommas;
        ReadCommentHandling = readCommentHandling;
    }

    /// <summary>
    /// Gets the default options (CamelCase naming, compact output).
    /// </summary>
    public static FastJsonOptionsModel Default => new();

    /// <inheritdoc />
    public bool Equals(FastJsonOptionsModel other)
    {
        return PropertyNamingPolicy == other.PropertyNamingPolicy &&
               WriteIndented == other.WriteIndented &&
               IgnoreReadOnlyProperties == other.IgnoreReadOnlyProperties &&
               DefaultIgnoreCondition == other.DefaultIgnoreCondition &&
               PropertyNameCaseInsensitive == other.PropertyNameCaseInsensitive &&
               AllowTrailingCommas == other.AllowTrailingCommas &&
               ReadCommentHandling == other.ReadCommentHandling;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is FastJsonOptionsModel other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + PropertyNamingPolicy.GetHashCode();
            hash = hash * 31 + WriteIndented.GetHashCode();
            hash = hash * 31 + IgnoreReadOnlyProperties.GetHashCode();
            hash = hash * 31 + DefaultIgnoreCondition.GetHashCode();
            hash = hash * 31 + PropertyNameCaseInsensitive.GetHashCode();
            hash = hash * 31 + AllowTrailingCommas.GetHashCode();
            hash = hash * 31 + ReadCommentHandling.GetHashCode();
            return hash;
        }
    }

    /// <summary>Equality operator.</summary>
    public static bool operator ==(FastJsonOptionsModel left, FastJsonOptionsModel right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(FastJsonOptionsModel left, FastJsonOptionsModel right) => !left.Equals(right);
}
