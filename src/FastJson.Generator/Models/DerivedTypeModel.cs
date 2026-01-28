using System;

namespace FastJson.Generator.Models;

/// <summary>
/// Value-equatable model representing a derived type for polymorphic serialization.
/// </summary>
public readonly struct DerivedTypeModel : IEquatable<DerivedTypeModel>
{
    /// <summary>
    /// Gets the fully qualified name of the derived type.
    /// </summary>
    public string TypeFullyQualifiedName { get; }

    /// <summary>
    /// Gets the type discriminator value (string or int).
    /// </summary>
    public string? TypeDiscriminator { get; }

    /// <summary>
    /// Gets whether the type discriminator is a string (true) or int (false).
    /// </summary>
    public bool IsStringDiscriminator { get; }

    public DerivedTypeModel(
        string typeFullyQualifiedName,
        string? typeDiscriminator,
        bool isStringDiscriminator)
    {
        TypeFullyQualifiedName = typeFullyQualifiedName;
        TypeDiscriminator = typeDiscriminator;
        IsStringDiscriminator = isStringDiscriminator;
    }

    public bool Equals(DerivedTypeModel other)
    {
        return TypeFullyQualifiedName == other.TypeFullyQualifiedName &&
               TypeDiscriminator == other.TypeDiscriminator &&
               IsStringDiscriminator == other.IsStringDiscriminator;
    }

    public override bool Equals(object? obj)
    {
        return obj is DerivedTypeModel other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = TypeFullyQualifiedName.GetHashCode();
            hash = hash * 31 + (TypeDiscriminator?.GetHashCode() ?? 0);
            hash = hash * 31 + IsStringDiscriminator.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(DerivedTypeModel left, DerivedTypeModel right) => left.Equals(right);
    public static bool operator !=(DerivedTypeModel left, DerivedTypeModel right) => !left.Equals(right);
}
