using Audio.Conversion;
using System.Text.Json.Serialization;

namespace Audio.Entries;
public abstract record AudioEntry : Entry, IDisposable
{
    private RIFFStream? _stream;

    [JsonIgnore]
    public AudioManager? Manager { get; set; }
    [JsonIgnore]
    public bool Convert => Manager?.Convert ?? false;
    [JsonIgnore]
    public string Extension => Convert && _stream != null ? _stream.Extension : ".wem";
    public override string? Location => $"{base.Location}{Extension}";

    protected AudioEntry(EntryType type) : base(type) { }

    public override bool TryWrite(Stream outStream)
    {
        if (Convert)
        {
            try
            {
                if (_stream == null)
                {
                    MemoryStream ms = new();
                    if (base.TryWrite(ms) && RIFFStream.TryParse(ms, out RIFFStream? audioStream))
                    {
                        _stream = audioStream;
                    }
                }

                _stream?.CopyTo(outStream);
            }
            catch (Exception e)
            {
                Logger.Error($"Error while converting audio file {Name}, {e}");
                return false;
            }

            return true;
        }

        return base.TryWrite(outStream);
    }

    public void Dispose()
    {
        _stream?.Dispose();
        _stream = null;

        GC.SuppressFinalize(this);
    }
}
