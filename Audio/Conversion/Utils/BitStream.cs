using System.Buffers;
using System.Buffers.Binary;

namespace Audio.Conversion.Utils;
public class BitStream : Stream
{
    private readonly Stream _baseStream;
    private readonly bool _leaveOpen;
    private readonly bool _writable;

    private int _position;
    private int _index;

    public BitStream(Stream stream, bool writable = true, bool leaveOpen = false)
    {
        _baseStream = stream ?? throw new ArgumentNullException(nameof(stream));
        _leaveOpen = leaveOpen;
        _writable = writable;

        _position = 0;
        _index = 0;
    }

    public virtual Stream BaseStream => _baseStream;
    public override bool CanRead => _baseStream.CanRead;
    public override bool CanSeek => _baseStream.CanSeek;
    public override bool CanWrite => _baseStream.CanWrite && _writable;
    public override long Length => _baseStream.Length * 8;
    public override long Position
    {
        get => _position * 8 + _index;
        set => Seek(value, SeekOrigin.Begin);
    }

    public override void Flush()
    {
        if (_index != 0)
        {
            Write(8 - _index);
        }
    }
    public override void SetLength(long value) => _baseStream.SetLength(value / 8);
    public override long Seek(long offset, SeekOrigin origin)
    {
        long position = offset / 8;
        long index = offset % 8;

        long newPosition = origin switch
        {
            SeekOrigin.Begin => position,
            SeekOrigin.Current => _position + position,
            SeekOrigin.End => Length + position,
            _ => throw new ArgumentOutOfRangeException(nameof(origin), "Invalid seek origin")
        };

        long newIndex = origin switch
        {
            SeekOrigin.Begin or SeekOrigin.End => index,
            SeekOrigin.Current => _index + index,
            _ => throw new ArgumentOutOfRangeException(nameof(origin), "Invalid seek origin")
        };

        if (newPosition < 0 || newPosition > Length)
            throw new IOException("Cannot seek to a given position");

        if (newIndex < 0 || newIndex > 8)
            throw new IOException("Cannot seek to a given index");

        _position = (int)newPosition;
        _index = (int)newIndex;
        return Position;
    }
    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfLessThan(offset, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan((buffer.Length * 8) - offset, count);
        ArgumentOutOfRangeException.ThrowIfLessThan(Length - Position, count);

        int byteIndex = offset / 8;
        int bitIndex = offset % 8;

        int byteCount = count / 8;
        int bitCount = count % 8;

        int alignedCount = (count + 7 & ~7) / 8;
        int alignedbyteCount = (count + _index + 7 & ~7) / 8;

        if (alignedbyteCount > 0)
        {
            byte[] tempBuffer = ArrayPool<byte>.Shared.Rent(alignedbyteCount);
            try
            {
                _baseStream.Position = _position;
                _baseStream.Read(tempBuffer.AsSpan(byteIndex, alignedbyteCount));
                _position += byteCount;

                if (alignedbyteCount != byteCount)
                {
                    int remaining = count;
                    for (int i = byteIndex; i < alignedbyteCount; i++)
                    {
                        tempBuffer[i] >>= _index;
                        tempBuffer[i] <<= bitIndex;

                        if (i != alignedbyteCount - 1)
                        {
                            tempBuffer[i] |= (byte)(tempBuffer[i + 1] << (8 - _index) << bitIndex);
                        }

                        int shiftCount = Math.Min(8, remaining);
                        tempBuffer[i] &= (byte)(byte.MaxValue >> (8 - shiftCount));
                        remaining -= shiftCount;
                    }

                    _index += bitCount;
                    if (_index >= 8)
                    {
                        _position++;
                        _index %= 8;
                    }
                }

                tempBuffer.AsSpan(byteIndex, alignedCount).CopyTo(buffer.AsSpan(byteIndex, alignedCount));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(tempBuffer);
            }
        }

        return count;
    }
    public override void Write(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfLessThan(offset, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan((buffer.Length * 8) - offset, count);

        if (Position + count > Length)
            count = (int)(Length - Position);

        int byteIndex = offset / 8;
        int bitIndex = offset % 8;

        int byteCount = count / 8;
        int bitCount = count % 8;

        int alignedCount = (count + 7 & ~7) / 8;
        int alignedbyteCount = (count + _index + 7 & ~7) / 8;

        if (alignedbyteCount > 0)
        {
            byte[] tempBuffer = ArrayPool<byte>.Shared.Rent(alignedbyteCount);
            try
            {
                buffer.AsSpan(byteIndex, alignedCount).CopyTo(tempBuffer.AsSpan(byteIndex, alignedCount));

                if (alignedbyteCount != byteCount)
                {
                    int remaining = bitCount;
                    for (int i = alignedbyteCount; i >= byteIndex; i--)
                    {
                        tempBuffer[i] <<= _index;
                        tempBuffer[i] <<= bitIndex;

                        if (i != byteIndex)
                        {
                            tempBuffer[i] |= (byte)(tempBuffer[i - 1] >> (8 - _index) << bitIndex);
                        }

                        int shiftCount = Math.Min(8, remaining);
                        tempBuffer[i] &= (byte)(byte.MaxValue >> (8 - shiftCount));
                        remaining += 8;
                    }

                    if (_index != 0)
                    {
                        byte value = 0;
                        _baseStream.Position = _position;
                        _baseStream.Read(new Span<byte>(ref value));

                        tempBuffer[byteIndex] |= (byte)(value & byte.MaxValue >> (8 - _index));
                    }

                    _index += bitCount;
                }

                _baseStream.Position = _position;
                _baseStream.Write(tempBuffer.AsSpan(byteIndex, alignedbyteCount));
                _position += byteCount;

                if (_index >= 8)
                {
                    _position++;
                    _index %= 8;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(tempBuffer);
            }
        }
    }
    public uint Read(int count)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, sizeof(uint) * 8, nameof(count));

        byte[] buffer = new byte[4];
        Read(buffer, 0, count);
        return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
    }
    public void Write(int count, uint value = 0)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, sizeof(uint) * 8, nameof(count));

        byte[] buffer = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        Write(buffer, 0, count);
    }
    protected override void Dispose(bool disposing)
    {
        Flush();
        if (!_leaveOpen)
        {
            _baseStream.Dispose();
        }
    }
}