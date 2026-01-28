using System;
using System.Buffers;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace FastJson;

/// <summary>
/// Zero-configuration AOT-compatible JSON serializer.
/// Uses direct Utf8JsonWriter calls for maximum performance.
/// </summary>
public static class FastJson
{
    [ThreadStatic] private static PooledBufferWriter? _buffer;

    /// <summary>
    /// Serializes the specified value to a JSON string.
    /// </summary>
    public static string Serialize<T>(T value)
    {
        var write = FastJsonWriter<T>.Write;
        if (write is null)
        {
            ThrowTypeNotRegistered<T>();
        }

        var buffer = _buffer ??= new PooledBufferWriter(256);
        buffer.Clear();

        using var writer = new Utf8JsonWriter(buffer);
        write!(writer, value);
        writer.Flush();

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>
    /// Serializes the specified value to a UTF-8 byte array.
    /// </summary>
    public static byte[] SerializeToUtf8Bytes<T>(T value)
    {
        var write = FastJsonWriter<T>.Write;
        if (write is null)
        {
            ThrowTypeNotRegistered<T>();
        }

        var buffer = _buffer ??= new PooledBufferWriter(256);
        buffer.Clear();

        using var writer = new Utf8JsonWriter(buffer);
        write!(writer, value);
        writer.Flush();

        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// Serializes the specified value to a UTF-8 byte span.
    /// Returns the number of bytes written.
    /// </summary>
    public static int Serialize<T>(T value, Span<byte> destination)
    {
        var write = FastJsonWriter<T>.Write;
        if (write is null)
        {
            ThrowTypeNotRegistered<T>();
        }

        var buffer = _buffer ??= new PooledBufferWriter(256);
        buffer.Clear();

        using var writer = new Utf8JsonWriter(buffer);
        write!(writer, value);
        writer.Flush();

        var written = buffer.WrittenSpan;
        if (written.Length > destination.Length)
        {
            throw new ArgumentException($"Destination buffer is too small. Required: {written.Length}, Available: {destination.Length}");
        }

        written.CopyTo(destination);
        return written.Length;
    }

    /// <summary>
    /// Deserializes the specified JSON string to a JsonNode.
    /// </summary>
    public static JsonNode? Deserialize(string json)
    {
        return JsonNode.Parse(json);
    }

    /// <summary>
    /// Deserializes the specified UTF-8 JSON bytes to a JsonNode.
    /// </summary>
    public static JsonNode? Deserialize(ReadOnlySpan<byte> utf8Json)
    {
        var reader = new Utf8JsonReader(utf8Json);
        return JsonNode.Parse(ref reader);
    }

    /// <summary>
    /// Deserializes the specified JSON string to an object of type T.
    /// </summary>
    public static T? Deserialize<T>(string json)
    {
        var read = FastJsonReader<T>.Read;
        if (read is null)
        {
            ThrowTypeNotRegistered<T>();
        }

        var bytes = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(bytes);
        return read!(ref reader);
    }

    /// <summary>
    /// Deserializes the specified UTF-8 JSON bytes to an object of type T.
    /// </summary>
    public static T? Deserialize<T>(ReadOnlySpan<byte> utf8Json)
    {
        var read = FastJsonReader<T>.Read;
        if (read is null)
        {
            ThrowTypeNotRegistered<T>();
        }

        var reader = new Utf8JsonReader(utf8Json);
        return read!(ref reader);
    }

    /// <summary>
    /// Asynchronously serializes the specified value to a stream.
    /// </summary>
    public static async Task SerializeAsync<T>(Stream stream, T value, CancellationToken cancellationToken = default)
    {
        var write = FastJsonWriter<T>.Write;
        if (write is null)
        {
            ThrowTypeNotRegistered<T>();
        }

        using var buffer = new PooledBufferWriter(256);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            write!(writer, value);
            writer.Flush();
        }

        await stream.WriteAsync(buffer.WrittenMemory, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously deserializes the specified stream to an object of type T.
    /// </summary>
    public static async ValueTask<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        var bytes = memoryStream.ToArray();
        return DeserializeCore<T>(bytes);
    }

    private static T? DeserializeCore<T>(byte[] bytes)
    {
        var read = FastJsonReader<T>.Read;
        if (read is null)
        {
            ThrowTypeNotRegistered<T>();
        }

        var reader = new Utf8JsonReader(bytes);
        return read!(ref reader);
    }

    private static void ThrowTypeNotRegistered<T>()
    {
        throw new InvalidOperationException(
            $"Type {typeof(T)} is not registered for FastJson serialization. " +
            $"Add [assembly: FastJsonInclude(typeof({typeof(T).Name}))] to register it, or ensure FastJson.Serialize/Deserialize is called with this type.");
    }
}

/// <summary>
/// Generic cache for serialization delegates. Set by the source generator's module initializer.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class FastJsonWriter<T>
{
    /// <summary>
    /// The serialization delegate for type T.
    /// </summary>
    public static Action<Utf8JsonWriter, T>? Write;
}

/// <summary>
/// Delegate type for deserialization that takes a ref Utf8JsonReader.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public delegate T? FastJsonReadDelegate<T>(ref Utf8JsonReader reader);

/// <summary>
/// Generic cache for deserialization delegates. Set by the source generator's module initializer.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class FastJsonReader<T>
{
    /// <summary>
    /// The deserialization delegate for type T.
    /// </summary>
    public static FastJsonReadDelegate<T>? Read;
}
