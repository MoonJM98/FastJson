namespace FastJson;

/// <summary>
/// Specifies the naming policy for JSON property names during serialization.
/// </summary>
public enum NamingPolicy
{
    /// <summary>
    /// Property names are used as-is without modification.
    /// </summary>
    None,

    /// <summary>
    /// Property names are converted to camelCase (e.g., "userName").
    /// </summary>
    CamelCase,

    /// <summary>
    /// Property names are converted to snake_case (e.g., "user_name").
    /// </summary>
    SnakeCase,

    /// <summary>
    /// Property names are converted to kebab-case (e.g., "user-name").
    /// </summary>
    KebabCase
}
