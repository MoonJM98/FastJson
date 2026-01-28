using System;

namespace FastJson;

/// <summary>
/// Specifies the naming policy and matching options for JSON serialization/deserialization of a type.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class JsonNamingAttribute : Attribute
{
    /// <summary>
    /// Gets the naming policy for serialization.
    /// </summary>
    public NamingPolicy Policy { get; }

    /// <summary>
    /// Gets or sets whether property name matching during deserialization should be case-insensitive.
    /// Default is false.
    /// </summary>
    public bool IgnoreCase { get; set; }

    /// <summary>
    /// Gets or sets whether property name matching during deserialization should ignore
    /// special characters (underscores and hyphens). When true, "user_name", "user-name",
    /// and "userName" will all match a property named "UserName".
    /// Default is false.
    /// </summary>
    public bool IgnoreSpecialCharacters { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonNamingAttribute"/> class.
    /// </summary>
    /// <param name="policy">The naming policy to apply during serialization.</param>
    public JsonNamingAttribute(NamingPolicy policy = NamingPolicy.None)
    {
        Policy = policy;
    }
}
