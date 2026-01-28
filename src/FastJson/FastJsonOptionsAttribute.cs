using System;

namespace FastJson;

/// <summary>
/// Configures JSON serialization options for the FastJson source generator.
/// Apply this attribute at the assembly level.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class FastJsonOptionsAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the property naming policy.
    /// Supported values: "CamelCase" (default), "PascalCase", "SnakeCaseLower", "SnakeCaseUpper", "KebabCaseLower", "KebabCaseUpper"
    /// </summary>
    public string PropertyNamingPolicy { get; set; } = "CamelCase";

    /// <summary>
    /// Gets or sets a value indicating whether JSON should be formatted with indentation.
    /// </summary>
    public bool WriteIndented { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to ignore read-only properties during serialization.
    /// </summary>
    public bool IgnoreReadOnlyProperties { get; set; } = false;

    /// <summary>
    /// Gets or sets the default value handling behavior.
    /// When true, properties with default values are not serialized.
    /// </summary>
    public bool DefaultIgnoreCondition { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether property name matching is case-insensitive during deserialization.
    /// </summary>
    public bool PropertyNameCaseInsensitive { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to allow trailing commas in JSON.
    /// </summary>
    public bool AllowTrailingCommas { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to allow comments in JSON.
    /// </summary>
    public bool ReadCommentHandling { get; set; } = false;
}
