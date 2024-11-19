using Audio.Conversion;
using System.Text.Json.Serialization;

namespace Audio.Entries;
public abstract record AudioEntry : Entry, IDisposable
{
    private RIFFHeader? _header;

    [JsonIgnore]
    public AudioManager? Manager { get; set; }
    [JsonIgnore]
    public bool Convert => Manager?.Convert ?? false;
    [JsonIgnore]
    public string Extension => Convert ? Header?.Extension ?? ".wem" : ".wem";
    public override string? Location => $"{base.Location}{Extension}";

    private RIFFHeader? Header
    {
        get
        {
            if (_header == null)
            {
                MemoryStream ms = new();
                if (base.TryWrite(ms))
                {
                    _header = RIFFHeader.Parse(ms);
                }
            }

            return _header;
        }
    }

    protected AudioEntry(EntryType type) : base(type) { }

    public override bool TryWrite(Stream outStream)
    {
        if (Convert)
        {
            try
            {
                MemoryStream ms = new();
                if (base.TryWrite(ms) && Header?.TryGetStream(ms, out RIFFStream? stream) == true)
                {
                    stream?.CopyTo(outStream);
                }
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
        GC.SuppressFinalize(this);
    }
}
