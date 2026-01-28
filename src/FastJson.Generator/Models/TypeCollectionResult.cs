using System;
using System.Collections.Generic;

namespace FastJson.Generator.Models;

/// <summary>
/// Result of type collection including types and diagnostic information.
/// </summary>
public readonly struct TypeCollectionResult : IEquatable<TypeCollectionResult>
{
    /// <summary>
    /// Maximum allowed nesting depth.
    /// </summary>
    public const int MaxDepth = 20;

    /// <summary>
    /// Maximum allowed type count.
    /// </summary>
    public const int MaxTypeCount = 500;

    /// <summary>
    /// Gets the collected types.
    /// </summary>
    public EquatableArray<TypeModel> Types { get; }

    /// <summary>
    /// Gets whether the max type count was exceeded.
    /// </summary>
    public bool TypeCountExceeded { get; }

    /// <summary>
    /// Gets the actual type count if it exceeded the maximum.
    /// </summary>
    public int ActualTypeCount { get; }

    /// <summary>
    /// Gets whether the max depth was exceeded.
    /// </summary>
    public bool DepthExceeded { get; }

    /// <summary>
    /// Gets the type name that exceeded the depth limit.
    /// </summary>
    public string? DepthExceededTypeName { get; }

    /// <summary>
    /// Gets the actual depth if it exceeded the maximum.
    /// </summary>
    public int ActualDepth { get; }

    public TypeCollectionResult(
        EquatableArray<TypeModel> types,
        bool typeCountExceeded = false,
        int actualTypeCount = 0,
        bool depthExceeded = false,
        string? depthExceededTypeName = null,
        int actualDepth = 0)
    {
        Types = types;
        TypeCountExceeded = typeCountExceeded;
        ActualTypeCount = actualTypeCount;
        DepthExceeded = depthExceeded;
        DepthExceededTypeName = depthExceededTypeName;
        ActualDepth = actualDepth;
    }

    public bool Equals(TypeCollectionResult other)
    {
        return Types == other.Types &&
               TypeCountExceeded == other.TypeCountExceeded &&
               DepthExceeded == other.DepthExceeded;
    }

    public override bool Equals(object? obj)
    {
        return obj is TypeCollectionResult other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Types.GetHashCode();
            hash = hash * 31 + TypeCountExceeded.GetHashCode();
            hash = hash * 31 + DepthExceeded.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(TypeCollectionResult left, TypeCollectionResult right) => left.Equals(right);
    public static bool operator !=(TypeCollectionResult left, TypeCollectionResult right) => !left.Equals(right);
}
