using System;
using System.Buffers;
using System.Collections.Generic;
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

    private static readonly List<IFastJsonContext> _contexts = new();

    /// <summary>
    /// Type-specific cache for writer delegates resolved from contexts.
    /// </summary>
    private static class WriterCache<T>
    {
        public static Action<Utf8JsonWriter, T>? Writer;
        public static bool Resolved;
    }

    /// <summary>
    /// Type-specific cache for reader delegates resolved from contexts.
    /// </summary>
    private static class ReaderCache<T>
    {
        public static FastJsonReadDelegate<T>? Reader;
        public static bool Resolved;
    }

    /// <summary>
    /// Type-specific cache for inline context that handles this type.
    /// </summary>
    private static class ContextCache<T>
    {
        public static IFastJsonContext? Context;
        public static bool Resolved;
    }

    /// <summary>
    /// Registers a FastJson context. Called by generated module initializers.
    /// </summary>
    /// <param name="context">The context to register.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterContext(IFastJsonContext context)
    {
        _contexts.Add(context);
    }

    private static Action<Utf8JsonWriter, T>? GetWriter<T>()
    {
        // Check cache first
        if (WriterCache<T>.Resolved)
            return WriterCache<T>.Writer;

        // Search registered contexts
        foreach (var ctx in _contexts)
        {
            if (ctx.TryGetWriter<T>(out var writer))
            {
                WriterCache<T>.Writer = writer;
                WriterCache<T>.Resolved = true;
                return writer;
            }
        }

        // Fallback to legacy direct registration
        if (FastJsonWriter<T>.Write != null)
        {
            WriterCache<T>.Writer = FastJsonWriter<T>.Write;
            WriterCache<T>.Resolved = true;
            return FastJsonWriter<T>.Write;
        }

        WriterCache<T>.Resolved = true;
        return null;
    }

    private static FastJsonReadDelegate<T>? GetReader<T>()
    {
        // Check cache first
        if (ReaderCache<T>.Resolved)
            return ReaderCache<T>.Reader;

        // Search registered contexts
        foreach (var ctx in _contexts)
        {
            if (ctx.TryGetReader<T>(out var reader))
            {
                ReaderCache<T>.Reader = reader;
                ReaderCache<T>.Resolved = true;
                return reader;
            }
        }

        // Fallback to legacy direct registration
        if (FastJsonReader<T>.Read != null)
        {
            ReaderCache<T>.Reader = FastJsonReader<T>.Read;
            ReaderCache<T>.Resolved = true;
            return FastJsonReader<T>.Read;
        }

        ReaderCache<T>.Resolved = true;
        return null;
    }

    /// <summary>
    /// Serializes the specified value to a JSON string.
    /// </summary>
    public static string Serialize<T>(T value)
    {
        // Try cached inline context first (fastest path)
        if (!ContextCache<T>.Resolved)
        {
            ResolveContextCache<T>();
        }

        if (ContextCache<T>.Context != null)
        {
            var result = ContextCache<T>.Context.Serialize(value);
            if (result != null)
                return result;
        }

        // Fallback to delegate-based approach
        var write = GetWriter<T>();
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

    private static void ResolveContextCache<T>()
    {
        foreach (var ctx in _contexts)
        {
            if (ctx.TryGetWriter<T>(out _))
            {
                ContextCache<T>.Context = ctx;
                break;
            }
        }
        ContextCache<T>.Resolved = true;
    }

    /// <summary>
    /// Serializes the specified value to a UTF-8 byte array.
    /// </summary>
    public static byte[] SerializeToUtf8Bytes<T>(T value)
    {
        var write = GetWriter<T>();
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
        var write = GetWriter<T>();
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
        // Try cached inline context first (fastest path)
        if (!ContextCache<T>.Resolved)
        {
            ResolveContextCache<T>();
        }

        if (ContextCache<T>.Context != null)
        {
            var (success, result) = ContextCache<T>.Context.Deserialize<T>(json);
            if (success)
                return result;
        }

        // Fallback to delegate-based approach
        var read = GetReader<T>();
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
        var read = GetReader<T>();
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
        var write = GetWriter<T>();
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
        var read = GetReader<T>();
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

    static FastJsonWriter()
    {
        // Register built-in handler for object type
        if (typeof(T) == typeof(object))
        {
            Write = (Action<Utf8JsonWriter, T>)(object)(Action<Utf8JsonWriter, object?>)((w, v) =>
            {
                if (v is null)
                {
                    w.WriteNullValue();
                }
                else
                {
                    JsonSerializer.Serialize(w, v, v.GetType());
                }
            });
        }
    }
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

    static FastJsonReader()
    {
        // Register built-in handler for object type - returns JsonNode
        if (typeof(T) == typeof(object))
        {
            Read = (FastJsonReadDelegate<T>)(object)(FastJsonReadDelegate<object?>)((ref Utf8JsonReader r) =>
            {
                return JsonNode.Parse(ref r);
            });
        }
    }
}
