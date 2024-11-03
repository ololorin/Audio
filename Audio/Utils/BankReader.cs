using Audio.Chunks;
using System.Text;

namespace Audio;

public class BankReader : BinaryReader
{
    private uint _version;

    public string? Source { get; set; }
    public BKHD? Root { get; set; }
    public uint Version
    {
        get
        {
            if (_version == 0 && Root is BKHD bkhd)
            {
                _version = bkhd.Version;
            }

            return _version;
        }
    }

    public BankReader(Stream stream, bool leaveOpen = true) : base(stream, Encoding.UTF8, leaveOpen) { }
    public BankReader(string path, bool leaveOpen = true) : this(File.OpenRead(path), leaveOpen)
    {
        Source = path;
    }
}