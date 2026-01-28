using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace FastJson.Generator.Models;

/// <summary>
/// An immutable array wrapper that implements value equality.
/// Used for incremental generator caching to ensure proper change detection.
/// </summary>
/// <typeparam name="T">The element type. Must implement <see cref="IEquatable{T}"/>.</typeparam>
/// <remarks>
/// <para>
/// Incremental generators require value equality for all cached data to work correctly.
/// Standard arrays use reference equality, which would cause unnecessary regeneration.
/// This wrapper compares arrays element-by-element for proper caching behavior.
/// </para>
/// <para>
/// This type is a readonly struct to avoid allocations when passing between pipeline stages.
/// </para>
/// </remarks>
public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly T[]? _array;

    /// <summary>
    /// Initializes a new instance from an array.
    /// </summary>
    /// <param name="array">The array to wrap. Can be null.</param>
    public EquatableArray(T[]? array)
    {
        _array = array;
    }

    /// <summary>
    /// Initializes a new instance from an immutable array.
    /// </summary>
    /// <param name="array">The immutable array to copy.</param>
    public EquatableArray(ImmutableArray<T> array)
    {
        _array = array.IsDefault ? null : array.ToArray();
    }

    /// <summary>
    /// Gets an empty array instance.
    /// </summary>
    public static EquatableArray<T> Empty => new(Array.Empty<T>());

    /// <summary>
    /// Gets the number of elements in the array.
    /// </summary>
    public int Length => _array?.Length ?? 0;

    /// <summary>
    /// Gets the element at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the element.</param>
    /// <returns>The element at the specified index.</returns>
    public T this[int index] => _array![index];

    /// <summary>
    /// Gets whether this instance wraps a null array.
    /// </summary>
    public bool IsDefault => _array is null;

    /// <summary>
    /// Gets the underlying array, or null if this instance is default.
    /// </summary>
    /// <returns>The underlying array.</returns>
    public T[]? AsArray() => _array;

    /// <summary>
    /// Gets a read-only span over the array elements.
    /// </summary>
    /// <returns>A span over the elements.</returns>
    public ReadOnlySpan<T> AsSpan() => _array.AsSpan();

    /// <summary>
    /// Determines whether this array equals another by comparing elements.
    /// </summary>
    /// <param name="other">The other array to compare.</param>
    /// <returns>True if the arrays have the same elements in the same order.</returns>
    public bool Equals(EquatableArray<T> other)
    {
        if (_array is null && other._array is null)
            return true;

        if (_array is null || other._array is null)
            return false;

        if (_array.Length != other._array.Length)
            return false;

        for (int i = 0; i < _array.Length; i++)
        {
            if (!_array[i].Equals(other._array[i]))
                return false;
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is EquatableArray<T> other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        if (_array is null)
            return 0;

        int hash = 17;
        foreach (var item in _array)
        {
            hash = hash * 31 + item.GetHashCode();
        }
        return hash;
    }

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator()
    {
        return (_array ?? Array.Empty<T>()).AsEnumerable().GetEnumerator();
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Equality operator.</summary>
    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);
}

/// <summary>
/// Extension methods for creating <see cref="EquatableArray{T}"/> instances.
/// </summary>
public static class EquatableArrayExtensions
{
    /// <summary>
    /// Creates an <see cref="EquatableArray{T}"/> from an enumerable sequence.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <returns>An equatable array containing the elements.</returns>
    public static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T> source) where T : IEquatable<T>
    {
        return new EquatableArray<T>(source.ToArray());
    }

    /// <summary>
    /// Creates an <see cref="EquatableArray{T}"/> from an immutable array.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source immutable array.</param>
    /// <returns>An equatable array containing the elements.</returns>
    public static EquatableArray<T> ToEquatableArray<T>(this ImmutableArray<T> source) where T : IEquatable<T>
    {
        return new EquatableArray<T>(source);
    }
}
