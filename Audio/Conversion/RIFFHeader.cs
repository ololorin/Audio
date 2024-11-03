using Audio.Conversion.Chunks;
using Audio.Extensions;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Audio.Conversion;
public record RIFFHeader
{
    public static string Signature = "RIFF";

    private readonly long _offset;
    private readonly Dictionary<string, WAVEChunk> _chunks = [];

    public long Offset => _offset;

    public RIFFHeader(Stream stream)
    {
        _offset = stream.Position;
    }

    public bool GetChunk<T>([NotNullWhen(true)] out T? chunk) where T : WAVEChunk
    {
        string signature = typeof(T) switch
        {
            Type _ when typeof(T) == typeof(FMT) => FMT.Signature,
            Type _ when typeof(T) == typeof(AKD) => AKD.Signature,
            Type _ when typeof(T) == typeof(VORB) => VORB.Signature,
            Type _ when typeof(T) == typeof(JUNK) => JUNK.Signature,
            Type _ when typeof(T) == typeof(DATA) => DATA.Signature,
            _ => throw new NotImplementedException(),
        };

        if (_chunks.TryGetValue(signature, out WAVEChunk? chk))
        {
            chunk = (T)chk;
            return true;
        }

        chunk = null;
        return false;
    }

    public static RIFFHeader Parse(Stream stream)
    {
        RIFFHeader header = new(stream);
        using BinaryReader reader = new(stream, Encoding.UTF8, true);

        string signature = reader.ReadRawString(4);
        if (signature != Signature)
        {
            throw new ArgumentException($"Invalid signature, Expected {Signature} got {signature}");
        }

        int size = reader.ReadInt32();
        string waveSignature = reader.ReadRawString(4);
        if (waveSignature != WAVEChunk.Signature)
        {
            throw new ArgumentException($"Invalid type signature, Expected {WAVEChunk.Signature} got {waveSignature}");
        }

        while (reader.BaseStream.Position < header.Offset + size)
        {
            if (WAVEChunk.TryParse(reader, out WAVEChunk? chunk))
            {
                header._chunks.Add(chunk.Header.Signature, chunk);
            }
        }

        if (header.GetChunk(out FMT? fmt) && fmt.ExtensionLength == 0x30)
        {
            HeaderInfo vorbHeader = new()
            {
                Signature = VORB.Signature,
                Length = (uint)fmt.ExtensionLength - 6
            };

            vorbHeader.Offset = fmt.Header.Offset + fmt.Header.Length - vorbHeader.Length;

            reader.BaseStream.Position = vorbHeader.Offset;
            VORB vorb = new(vorbHeader);
            vorb.Read(reader);

            vorbHeader.Align(reader);
            header._chunks.Add(VORB.Signature, vorb);
        }

        if (header.GetChunk(out DATA? data) && data.Header.Length > stream.Length)
        {
            Logger.Warning($"Truncated audio stream, Expected {data.Header.Length} got {stream.Length}, resizing...");
            data.Header.Length = (uint)(stream.Length - data.Header.Offset);
        }

        return header;
    }
}
