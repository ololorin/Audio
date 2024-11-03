using Audio.Conversion.Chunks;
using Audio.Conversion.Codecs;
using System.Diagnostics.CodeAnalysis;

namespace Audio.Conversion;
public class RIFFStream : Stream
{
    protected readonly Stream _baseStream;
    protected readonly long _offset;

    public virtual string Extension => ".wem";
    public RIFFHeader Header { get; private set; }
    public override bool CanRead => _baseStream.CanRead;
    public override bool CanSeek => _baseStream.CanSeek;
    public override bool CanWrite => _baseStream.CanWrite;
    public override long Length => _baseStream.Length;
    public override long Position
    {
        get => _baseStream.Position;
        set => _baseStream.Position = value;
    }

    public RIFFStream(Stream stream, RIFFHeader header)
    {
        _baseStream = stream;
        _offset = stream.Position;

        Header = header;
    }

    public override void Flush() => _baseStream.Flush();
    public override void SetLength(long value) => _baseStream.SetLength(value);
    public override int Read(byte[] buffer, int offset, int count) => _baseStream.Read(buffer, offset, count);
    public override void Write(byte[] buffer, int offset, int count) => _baseStream.Write(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);
    public override void CopyTo(Stream destination, int bufferSize = 81920)
    {
        try
        {
            _baseStream.Position = Header.Offset;
            base.CopyTo(destination, bufferSize);
        }
        catch (Exception e)
        {
            Logger.Warning($"Error while copying RIFF stream, {e}");
        }

        destination.Position = 0;
    }

    protected override void Dispose(bool disposing)
    {
        _baseStream?.Dispose();
    }

    public static bool TryParse(Stream inputStream, [NotNullWhen(true)] out RIFFStream? audioStream)
    {
        RIFFHeader header = RIFFHeader.Parse(inputStream);

        if (header.GetChunk(out FMT? fmt))
        {
            audioStream = fmt.Format switch
            {
                WAVEFormat.VORBIS => new Vorbis(inputStream, header),
                WAVEFormat.PTADPCM => new PTADPCM(inputStream, header),
                _ => new RIFFStream(inputStream, header),
            };

            return true;
        }

        audioStream = null;
        return false;
    }
}

