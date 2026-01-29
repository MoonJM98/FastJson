using System;
using System.Text.Json;

namespace FastJson;

/// <summary>
/// Interface for FastJson context that provides type-specific serializers and deserializers.
/// Each project using FastJson generates its own context implementing this interface.
/// </summary>
public interface IFastJsonContext
{
    /// <summary>
    /// Tries to get a writer delegate for the specified type.
    /// </summary>
    bool TryGetWriter<T>(out Action<Utf8JsonWriter, T>? writer);

    /// <summary>
    /// Tries to get a reader delegate for the specified type.
    /// </summary>
    bool TryGetReader<T>(out FastJsonReadDelegate<T>? reader);

    /// <summary>
    /// Serializes the value inline. Returns null if type not supported.
    /// </summary>
    string? Serialize<T>(T value);

    /// <summary>
    /// Deserializes the JSON inline. Returns (false, default) if type not supported.
    /// </summary>
    (bool Success, T? Value) Deserialize<T>(string json);
}
