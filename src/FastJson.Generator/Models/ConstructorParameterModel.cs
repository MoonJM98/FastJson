using System;

namespace FastJson.Generator.Models;

/// <summary>
/// Value-equatable model representing a constructor parameter for deserialization.
/// </summary>
public readonly struct ConstructorParameterModel : IEquatable<ConstructorParameterModel>
{
    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the JSON property name (matched to property).
    /// </summary>
    public string JsonName { get; }

    /// <summary>
    /// Gets the fully qualified type name.
    /// </summary>
    public string TypeFullyQualifiedName { get; }

    /// <summary>
    /// Gets the position in the constructor.
    /// </summary>
    public int Position { get; }

    /// <summary>
    /// Gets whether this parameter has a default value.
    /// </summary>
    public bool HasDefaultValue { get; }

    /// <summary>
    /// Gets the default value as a string (for code generation).
    /// </summary>
    public string? DefaultValueString { get; }

    public ConstructorParameterModel(
        string name,
        string jsonName,
        string typeFullyQualifiedName,
        int position,
        bool hasDefaultValue = false,
        string? defaultValueString = null)
    {
        Name = name;
        JsonName = jsonName;
        TypeFullyQualifiedName = typeFullyQualifiedName;
        Position = position;
        HasDefaultValue = hasDefaultValue;
        DefaultValueString = defaultValueString;
    }

    public bool Equals(ConstructorParameterModel other)
    {
        return Name == other.Name &&
               JsonName == other.JsonName &&
               TypeFullyQualifiedName == other.TypeFullyQualifiedName &&
               Position == other.Position &&
               HasDefaultValue == other.HasDefaultValue &&
               DefaultValueString == other.DefaultValueString;
    }

    public override bool Equals(object? obj)
    {
        return obj is ConstructorParameterModel other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + Name.GetHashCode();
            hash = hash * 31 + Position;
            return hash;
        }
    }

    public static bool operator ==(ConstructorParameterModel left, ConstructorParameterModel right) => left.Equals(right);
    public static bool operator !=(ConstructorParameterModel left, ConstructorParameterModel right) => !left.Equals(right);
}
