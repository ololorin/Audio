namespace Audio.Conversion.Chunks;

public record EnvelopePoint : IReadable<BinaryReader>
{
    public uint Position { get; set; }
    public ushort Attenuation { get; set; }

    public void Read(BinaryReader reader)
    {
        Position = reader.ReadUInt32();
        Attenuation = reader.ReadUInt16();
    }
}