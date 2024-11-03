namespace Audio.Chunks;
public record BKHD : Chunk
{
    public new const string Signature = "BKHD";

    public uint Version { get; set; }
    public FNVID<uint> ID { get; set; }
    public FNVID<uint> LangaugeID { get; set; }
    public ushort Alignment { get; set; }

    public BKHD(HeaderInfo header) : base(header)
    {
        ID = 0;
        LangaugeID = 0;
    }

    public override void Read(BankReader reader)
    {
        Version = reader.ReadUInt32();
        ID = reader.ReadUInt32();
        LangaugeID = reader.ReadUInt32();
        Alignment = reader.ReadUInt16();
    }
}