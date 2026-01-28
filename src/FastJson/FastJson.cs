using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace FastJson;

/// <summary>
/// Zero-configuration AOT-compatible JSON serializer.
/// </summary>
public static class FastJson
{
    private static bool _initialized;

    /// <summary>
    /// Marks FastJson as initialized. Called by the source generator's module initializer.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void MarkInitialized() => _initialized = true;

    /// <summary>
    /// Serializes the specified value to a JSON string.
    /// </summary>
    public static string Serialize<T>(T value)
    {
        var typeInfo = FastJsonCache<T>.TypeInfo;
        if (typeInfo is null)
        {
            if (!_initialized) ThrowNotInitialized();
            ThrowTypeNotRegistered<T>();
        }
        return JsonSerializer.Serialize(value, typeInfo!);
    }

    /// <summary>
    /// Deserializes the specified JSON string to an object of type T.
    /// </summary>
    public static T? Deserialize<T>(string json)
    {
        var typeInfo = FastJsonCache<T>.TypeInfo;
        if (typeInfo is null)
        {
            if (!_initialized) ThrowNotInitialized();
            ThrowTypeNotRegistered<T>();
        }
        return JsonSerializer.Deserialize(json, typeInfo!);
    }

    /// <summary>
    /// Asynchronously serializes the specified value to a stream.
    /// </summary>
    public static Task SerializeAsync<T>(Stream stream, T value, CancellationToken cancellationToken = default)
    {
        var typeInfo = FastJsonCache<T>.TypeInfo;
        if (typeInfo is null)
        {
            if (!_initialized) ThrowNotInitialized();
            ThrowTypeNotRegistered<T>();
        }
        return JsonSerializer.SerializeAsync(stream, value, typeInfo!, cancellationToken);
    }

    /// <summary>
    /// Asynchronously deserializes the specified stream to an object of type T.
    /// </summary>
    public static ValueTask<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        var typeInfo = FastJsonCache<T>.TypeInfo;
        if (typeInfo is null)
        {
            if (!_initialized) ThrowNotInitialized();
            ThrowTypeNotRegistered<T>();
        }
        return JsonSerializer.DeserializeAsync(stream, typeInfo!, cancellationToken);
    }

    private static void ThrowNotInitialized()
    {
        throw new InvalidOperationException(
            "FastJson source generator has not run. " +
            "Ensure the FastJson.Generator package is referenced and the project has been rebuilt.");
    }

    private static void ThrowTypeNotRegistered<T>()
    {
        throw new InvalidOperationException(
            $"Type {typeof(T)} is not registered for FastJson serialization. " +
            $"Add [assembly: FastJsonInclude(typeof({typeof(T).Name}))] to register it.");
    }
}

/// <summary>
/// Generic cache for JsonTypeInfo. Set by the source generator's module initializer.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class FastJsonCache<T>
{
    /// <summary>
    /// The cached JsonTypeInfo for type T.
    /// </summary>
    public static JsonTypeInfo<T>? TypeInfo;
}
