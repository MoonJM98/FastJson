using System;
using System.Buffers;

namespace FastJson;

/// <summary>
/// A buffer writer that uses ArrayPool for efficient memory management.
/// Reduces GC pressure by reusing byte arrays from the shared pool.
/// </summary>
public sealed class PooledBufferWriter : IBufferWriter<byte>, IDisposable
{
    private byte[] _buffer;
    private int _index;
    private bool _disposed;

    private const int DefaultInitialCapacity = 256;
    private const int MaxArrayLength = 0x7FFFFFC7; // Array.MaxLength

    public PooledBufferWriter(int initialCapacity = DefaultInitialCapacity)
    {
        if (initialCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));

        _buffer = ArrayPool<byte>.Shared.Rent(initialCapacity);
        _index = 0;
    }

    /// <summary>
    /// Gets the data written to the underlying buffer.
    /// </summary>
    public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _index);

    /// <summary>
    /// Gets the data written to the underlying buffer as memory.
    /// </summary>
    public ReadOnlyMemory<byte> WrittenMemory => _buffer.AsMemory(0, _index);

    /// <summary>
    /// Gets the number of bytes written.
    /// </summary>
    public int WrittenCount => _index;

    /// <summary>
    /// Gets the total capacity of the buffer.
    /// </summary>
    public int Capacity => _buffer.Length;

    /// <summary>
    /// Gets the number of bytes available for writing without resizing.
    /// </summary>
    public int FreeCapacity => _buffer.Length - _index;

    /// <summary>
    /// Clears the written data, allowing the buffer to be reused.
    /// </summary>
    public void Clear()
    {
        _index = 0;
    }

    /// <summary>
    /// Resets the buffer by returning the current array to the pool and renting a new one.
    /// </summary>
    public void Reset(int initialCapacity = DefaultInitialCapacity)
    {
        if (_buffer.Length > 0)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
        }
        _buffer = ArrayPool<byte>.Shared.Rent(initialCapacity);
        _index = 0;
    }

    public void Advance(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        if (_index > _buffer.Length - count)
            throw new InvalidOperationException("Cannot advance past the end of the buffer.");

        _index += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        CheckAndResizeBuffer(sizeHint);
        return _buffer.AsMemory(_index);
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        CheckAndResizeBuffer(sizeHint);
        return _buffer.AsSpan(_index);
    }

    private void CheckAndResizeBuffer(int sizeHint)
    {
        if (sizeHint < 0)
            throw new ArgumentOutOfRangeException(nameof(sizeHint));

        if (sizeHint == 0)
            sizeHint = 1;

        if (sizeHint > FreeCapacity)
        {
            int currentLength = _buffer.Length;
            int growBy = Math.Max(sizeHint, currentLength);

            int newSize = currentLength + growBy;

            if ((uint)newSize > MaxArrayLength)
            {
                newSize = Math.Max(currentLength + sizeHint, MaxArrayLength);
            }

            byte[] newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
            Array.Copy(_buffer, newBuffer, _index);
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = newBuffer;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_buffer.Length > 0)
            {
                ArrayPool<byte>.Shared.Return(_buffer);
            }
            _buffer = Array.Empty<byte>();
            _disposed = true;
        }
    }
}
