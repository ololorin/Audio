namespace Audio.Entries;
public record External : AudioEntry
{
    private FNVID<ulong> _id;

    public override ulong ID => _id.Value;
    public override string? Name => _id.ToString();
    public override string? Location => string.IsNullOrEmpty(_id.String) ? base.Location : Path.ChangeExtension(Name, Extension);

    public External() : base(EntryType.External)
    {
        _id = new();
    }

    public override void Read(BankReader reader)
    {
        _id = reader.ReadUInt64();

        base.Read(reader);
    }
}
