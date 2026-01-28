using System;
using System.Security.Cryptography;
using System.Text;

namespace FastJson.Generator.Models;

/// <summary>
/// Value-equatable model representing a type for JSON serialization.
/// Used for Incremental Generator caching.
/// </summary>
public readonly struct TypeModel : IEquatable<TypeModel>
{
    /// <summary>
    /// Gets the fully qualified name of the type (e.g., "global::System.Collections.Generic.List&lt;global::MyApp.User&gt;").
    /// </summary>
    public string FullyQualifiedName { get; }

    /// <summary>
    /// Gets the type name without namespace (e.g., "User", "List&lt;User&gt;").
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Gets the generated property name for JsonSerializerContext (e.g., "__FJ_User_A1B2C3").
    /// </summary>
    public string ContextPropertyName { get; }

    /// <summary>
    /// Gets whether this is a generic type.
    /// </summary>
    public bool IsGeneric { get; }

    /// <summary>
    /// Gets whether this type is a collection (List, Array, Dictionary, etc.).
    /// </summary>
    public bool IsCollection { get; }

    /// <summary>
    /// Gets whether this type is a value type (struct).
    /// </summary>
    public bool IsValueType { get; }

    /// <summary>
    /// Gets the namespace of the type.
    /// </summary>
    public string Namespace { get; }

    /// <summary>
    /// Gets the properties of the type (for object types).
    /// </summary>
    public EquatableArray<PropertyModel> Properties { get; }

    /// <summary>
    /// Gets the element type for collections/arrays (null for non-collection types).
    /// </summary>
    public string? ElementTypeName { get; }

    /// <summary>
    /// Gets the key type for dictionaries (null for non-dictionary types).
    /// </summary>
    public string? KeyTypeName { get; }

    /// <summary>
    /// Gets the value type for dictionaries (null for non-dictionary types).
    /// </summary>
    public string? ValueTypeName { get; }

    /// <summary>
    /// Gets whether the type has a parameterless constructor.
    /// </summary>
    public bool HasParameterlessConstructor { get; }

    /// <summary>
    /// Gets the constructor parameters for parameterized constructor deserialization.
    /// </summary>
    public EquatableArray<ConstructorParameterModel> ConstructorParameters { get; }

    /// <summary>
    /// Gets whether the type is a record type.
    /// </summary>
    public bool IsRecord { get; }

    /// <summary>
    /// Gets whether the type is an enum.
    /// </summary>
    public bool IsEnum { get; }

    /// <summary>
    /// Gets the fully qualified name of the custom JsonConverter type, if specified at type level.
    /// </summary>
    public string? ConverterTypeName { get; }

    /// <summary>
    /// Gets whether this type has [JsonPolymorphic] attribute.
    /// </summary>
    public bool IsPolymorphic { get; }

    /// <summary>
    /// Gets the type discriminator property name for polymorphic serialization (default "$type").
    /// </summary>
    public string? TypeDiscriminatorPropertyName { get; }

    /// <summary>
    /// Gets the derived types for polymorphic serialization.
    /// </summary>
    public EquatableArray<DerivedTypeModel> DerivedTypes { get; }

    /// <summary>
    /// Gets whether the type is abstract.
    /// </summary>
    public bool IsAbstract { get; }

    /// <summary>
    /// Gets the naming policy for property names during serialization.
    /// </summary>
    public int NamingPolicy { get; }

    /// <summary>
    /// Gets whether property name matching should be case-insensitive during deserialization.
    /// </summary>
    public bool IgnoreCase { get; }

    /// <summary>
    /// Gets whether property name matching should ignore special characters during deserialization.
    /// </summary>
    public bool IgnoreSpecialCharacters { get; }

    /// <summary>
    /// Gets whether this is an anonymous type (write-only serialization).
    /// </summary>
    public bool IsAnonymous { get; }

    public TypeModel(
        string fullyQualifiedName,
        string typeName,
        string @namespace,
        bool isGeneric,
        bool isCollection,
        bool isValueType,
        bool hasParameterlessConstructor,
        EquatableArray<PropertyModel> properties,
        string? elementTypeName = null,
        string? keyTypeName = null,
        string? valueTypeName = null,
        EquatableArray<ConstructorParameterModel>? constructorParameters = null,
        bool isRecord = false,
        bool isEnum = false,
        string? converterTypeName = null,
        bool isPolymorphic = false,
        string? typeDiscriminatorPropertyName = null,
        EquatableArray<DerivedTypeModel>? derivedTypes = null,
        bool isAbstract = false,
        int namingPolicy = 0,
        bool ignoreCase = false,
        bool ignoreSpecialCharacters = false,
        bool isAnonymous = false)
    {
        FullyQualifiedName = fullyQualifiedName;
        TypeName = typeName;
        Namespace = @namespace;
        IsGeneric = isGeneric;
        IsCollection = isCollection;
        IsValueType = isValueType;
        HasParameterlessConstructor = hasParameterlessConstructor;
        Properties = properties;
        ElementTypeName = elementTypeName;
        KeyTypeName = keyTypeName;
        ValueTypeName = valueTypeName;
        ConstructorParameters = constructorParameters ?? EquatableArray<ConstructorParameterModel>.Empty;
        IsRecord = isRecord;
        IsEnum = isEnum;
        ConverterTypeName = converterTypeName;
        IsPolymorphic = isPolymorphic;
        TypeDiscriminatorPropertyName = typeDiscriminatorPropertyName;
        DerivedTypes = derivedTypes ?? EquatableArray<DerivedTypeModel>.Empty;
        IsAbstract = isAbstract;
        NamingPolicy = namingPolicy;
        IgnoreCase = ignoreCase;
        IgnoreSpecialCharacters = ignoreSpecialCharacters;
        IsAnonymous = isAnonymous;
        ContextPropertyName = GenerateContextPropertyName(fullyQualifiedName, typeName);
    }

    private static string GenerateContextPropertyName(string fullyQualifiedName, string typeName)
    {
        // Generate a safe property name: __FJ_<SimplifiedName>_<Hash>
        var simplifiedName = SimplifyTypeName(typeName);
        var hash = ComputeShortHash(fullyQualifiedName);
        return $"__FJ_{simplifiedName}_{hash}";
    }

    private static string SimplifyTypeName(string typeName)
    {
        // Remove generic syntax and special characters
        var sb = new StringBuilder();
        foreach (var c in typeName)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private static string ComputeShortHash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);

        // Take first 6 characters of hex representation
        var sb = new StringBuilder(6);
        for (int i = 0; i < 3; i++)
        {
            sb.Append(hash[i].ToString("X2"));
        }
        return sb.ToString();
    }

    public bool Equals(TypeModel other)
    {
        return FullyQualifiedName == other.FullyQualifiedName &&
               Properties == other.Properties &&
               ConstructorParameters == other.ConstructorParameters &&
               IsRecord == other.IsRecord &&
               IsEnum == other.IsEnum &&
               ConverterTypeName == other.ConverterTypeName &&
               IsPolymorphic == other.IsPolymorphic &&
               TypeDiscriminatorPropertyName == other.TypeDiscriminatorPropertyName &&
               DerivedTypes == other.DerivedTypes &&
               IsAbstract == other.IsAbstract &&
               NamingPolicy == other.NamingPolicy &&
               IgnoreCase == other.IgnoreCase &&
               IgnoreSpecialCharacters == other.IgnoreSpecialCharacters &&
               IsAnonymous == other.IsAnonymous;
    }

    public override bool Equals(object? obj)
    {
        return obj is TypeModel other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = FullyQualifiedName.GetHashCode() * 31 + Properties.GetHashCode();
            hash = hash * 31 + ConstructorParameters.GetHashCode();
            hash = hash * 31 + IsRecord.GetHashCode();
            hash = hash * 31 + IsEnum.GetHashCode();
            hash = hash * 31 + (ConverterTypeName?.GetHashCode() ?? 0);
            hash = hash * 31 + IsPolymorphic.GetHashCode();
            hash = hash * 31 + (TypeDiscriminatorPropertyName?.GetHashCode() ?? 0);
            hash = hash * 31 + DerivedTypes.GetHashCode();
            hash = hash * 31 + IsAbstract.GetHashCode();
            hash = hash * 31 + NamingPolicy.GetHashCode();
            hash = hash * 31 + IgnoreCase.GetHashCode();
            hash = hash * 31 + IgnoreSpecialCharacters.GetHashCode();
            hash = hash * 31 + IsAnonymous.GetHashCode();
            return hash;
        }
    }

    public override string ToString()
    {
        return FullyQualifiedName;
    }

    public static bool operator ==(TypeModel left, TypeModel right) => left.Equals(right);
    public static bool operator !=(TypeModel left, TypeModel right) => !left.Equals(right);
}
