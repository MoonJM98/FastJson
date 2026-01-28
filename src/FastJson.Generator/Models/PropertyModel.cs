using System;

namespace FastJson.Generator.Models;

/// <summary>
/// Value-equatable model representing a property for JSON serialization.
/// </summary>
public readonly struct PropertyModel : IEquatable<PropertyModel>
{
    /// <summary>
    /// Gets the property name in C# code.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the JSON property name (camelCase by default).
    /// </summary>
    public string JsonName { get; }

    /// <summary>
    /// Gets the fully qualified type name of the property.
    /// </summary>
    public string TypeFullyQualifiedName { get; }

    /// <summary>
    /// Gets whether the property has a getter.
    /// </summary>
    public bool HasGetter { get; }

    /// <summary>
    /// Gets whether the property has a setter.
    /// </summary>
    public bool HasSetter { get; }

    /// <summary>
    /// Gets whether the property type is nullable.
    /// </summary>
    public bool IsNullable { get; }

    /// <summary>
    /// Gets whether this is a value type (struct).
    /// </summary>
    public bool IsValueType { get; }

    /// <summary>
    /// Gets whether this property should be ignored ([JsonIgnore]).
    /// </summary>
    public bool IsIgnored { get; }

    /// <summary>
    /// Gets whether this property is required ([JsonRequired]).
    /// </summary>
    public bool IsRequired { get; }

    /// <summary>
    /// Gets whether this property has [JsonInclude] (for private/field).
    /// </summary>
    public bool HasJsonInclude { get; }

    /// <summary>
    /// Gets whether this member is public.
    /// </summary>
    public bool IsPublic { get; }

    /// <summary>
    /// Gets whether this property has an init-only setter.
    /// </summary>
    public bool IsInitOnly { get; }

    /// <summary>
    /// Gets whether this is a field (not a property).
    /// </summary>
    public bool IsField { get; }

    /// <summary>
    /// Gets the fully qualified name of the custom JsonConverter type, if specified.
    /// </summary>
    public string? ConverterTypeName { get; }

    /// <summary>
    /// Gets the JsonNumberHandling value, if specified.
    /// </summary>
    public string? NumberHandling { get; }

    public PropertyModel(
        string name,
        string jsonName,
        string typeFullyQualifiedName,
        bool hasGetter,
        bool hasSetter,
        bool isNullable,
        bool isValueType,
        bool isIgnored = false,
        bool isRequired = false,
        bool hasJsonInclude = false,
        bool isPublic = true,
        bool isInitOnly = false,
        bool isField = false,
        string? converterTypeName = null,
        string? numberHandling = null)
    {
        Name = name;
        JsonName = jsonName;
        TypeFullyQualifiedName = typeFullyQualifiedName;
        HasGetter = hasGetter;
        HasSetter = hasSetter;
        IsNullable = isNullable;
        IsValueType = isValueType;
        IsIgnored = isIgnored;
        IsRequired = isRequired;
        HasJsonInclude = hasJsonInclude;
        IsPublic = isPublic;
        IsInitOnly = isInitOnly;
        IsField = isField;
        ConverterTypeName = converterTypeName;
        NumberHandling = numberHandling;
    }

    public bool Equals(PropertyModel other)
    {
        return Name == other.Name &&
               JsonName == other.JsonName &&
               TypeFullyQualifiedName == other.TypeFullyQualifiedName &&
               HasGetter == other.HasGetter &&
               HasSetter == other.HasSetter &&
               IsNullable == other.IsNullable &&
               IsValueType == other.IsValueType &&
               IsIgnored == other.IsIgnored &&
               IsRequired == other.IsRequired &&
               HasJsonInclude == other.HasJsonInclude &&
               IsPublic == other.IsPublic &&
               IsInitOnly == other.IsInitOnly &&
               IsField == other.IsField &&
               ConverterTypeName == other.ConverterTypeName &&
               NumberHandling == other.NumberHandling;
    }

    public override bool Equals(object? obj)
    {
        return obj is PropertyModel other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + Name.GetHashCode();
            hash = hash * 31 + JsonName.GetHashCode();
            hash = hash * 31 + TypeFullyQualifiedName.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(PropertyModel left, PropertyModel right) => left.Equals(right);
    public static bool operator !=(PropertyModel left, PropertyModel right) => !left.Equals(right);
}
