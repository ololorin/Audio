namespace Audio.Entries;
public record Sound : AudioEntry
{
    private FNVID<uint> _id;

    public override ulong ID => _id.Value;
    public override string? Name => _id.ToString();

    public Sound() : base(EntryType.Sound)
    {
        _id = new();
    }

    public override void Read(BankReader reader)
    {
        _id = reader.ReadUInt32();

        base.Read(reader);
    }
}