using Audio.Chunks;

namespace Audio.Entries;
public record EmbeddedSound : AudioEntry
{
    private Bank? _bank = null;
    private FNVID<uint> _id;

    public override ulong ID => _id.Value;
    public override string? Name => _id.ToString();
    public override string? Location => $"{Folder?.Name ?? "None"}/{_bank?.Name}/{Name}{Extension}";
    public Bank? Bank
    {
        set
        {
            if (_bank == null && value?.GetChunk(out DATA? data) == true)
            {
                _bank = value;
                Folder = _bank.Folder;
                Source = _bank.Source;
                Offset += data.BaseOffset;
            }
        }
    }

    public EmbeddedSound() : base(EntryType.EmbeddedSound)
    {
        _id = new();
    }

    public override void Read(BankReader reader)
    {
        _id = reader.ReadUInt32();
        Offset = reader.ReadUInt32();
        Size = reader.ReadInt32();
    }
}