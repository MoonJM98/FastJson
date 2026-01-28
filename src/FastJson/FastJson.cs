using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FastJson;

/// <summary>
/// Zero-configuration AOT-compatible JSON serializer.
/// </summary>
public static class FastJson
{
    // Delegates that will be set by the source generator via module initializer
    private static Func<object?, Type, string>? _serializeFunc;
    private static Func<string, Type, object?>? _deserializeFunc;
    private static Func<Stream, object?, Type, CancellationToken, Task>? _serializeAsyncFunc;
    private static Func<Stream, Type, CancellationToken, ValueTask<object?>>? _deserializeAsyncFunc;

    /// <summary>
    /// Configures FastJson with the generated serialization functions.
    /// This method is called automatically by the source generator's module initializer.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Configure(
        Func<object?, Type, string> serializeFunc,
        Func<string, Type, object?> deserializeFunc,
        Func<Stream, object?, Type, CancellationToken, Task> serializeAsyncFunc,
        Func<Stream, Type, CancellationToken, ValueTask<object?>> deserializeAsyncFunc)
    {
        _serializeFunc = serializeFunc;
        _deserializeFunc = deserializeFunc;
        _serializeAsyncFunc = serializeAsyncFunc;
        _deserializeAsyncFunc = deserializeAsyncFunc;
    }

    /// <summary>
    /// Serializes the specified value to a JSON string.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <returns>A JSON string representation of the value.</returns>
    public static string Serialize<T>(T value)
    {
        if (_serializeFunc is null)
            ThrowNotInitialized();
        return _serializeFunc!(value, typeof(T));
    }

    /// <summary>
    /// Deserializes the specified JSON string to an object of type T.
    /// </summary>
    /// <typeparam name="T">The type of the object to deserialize to.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized object, or null if the JSON represents null.</returns>
    public static T? Deserialize<T>(string json)
    {
        if (_deserializeFunc is null)
            ThrowNotInitialized();
        return (T?)_deserializeFunc!(json, typeof(T));
    }

    /// <summary>
    /// Asynchronously serializes the specified value to a stream.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="value">The value to serialize.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public static Task SerializeAsync<T>(Stream stream, T value, CancellationToken cancellationToken = default)
    {
        if (_serializeAsyncFunc is null)
            ThrowNotInitialized();
        return _serializeAsyncFunc!(stream, value, typeof(T), cancellationToken);
    }

    /// <summary>
    /// Asynchronously deserializes the specified stream to an object of type T.
    /// </summary>
    /// <typeparam name="T">The type of the object to deserialize to.</typeparam>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The deserialized object, or null if the JSON represents null.</returns>
    public static async ValueTask<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        if (_deserializeAsyncFunc is null)
            ThrowNotInitialized();
        var result = await _deserializeAsyncFunc!(stream, typeof(T), cancellationToken).ConfigureAwait(false);
        return (T?)result;
    }

    private static void ThrowNotInitialized()
    {
        throw new InvalidOperationException(
            "FastJson source generator has not run. " +
            "Ensure the FastJson.Generator package is referenced and the project has been rebuilt.");
    }
}
