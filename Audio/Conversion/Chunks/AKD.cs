namespace Audio.Conversion.Chunks;
public record AKD : WAVEChunk
{
    public new const string Signature = "akd ";

    public float LoudnessNormalizationGain { get; set; }
    public float DownmixNormalizationGain { get; set; }
    public uint EnvelopePointsCount { get; set; }
    public float EnvelopePeak { get; set; }
    public EnvelopePoint[] EnvelopePoints { get; set; } = [];

    public AKD(HeaderInfo header) : base(header) { }

    public override void Read(BinaryReader reader)
    {
        LoudnessNormalizationGain = reader.ReadSingle();
        DownmixNormalizationGain = reader.ReadSingle();
        EnvelopePointsCount = reader.ReadUInt32();
        EnvelopePeak = reader.ReadSingle();

        EnvelopePoints = new EnvelopePoint[EnvelopePointsCount];
        for (int i = 0; i < EnvelopePointsCount; i++)
        {
            EnvelopePoints[i] = new();
            EnvelopePoints[i].Read(reader);
        }
    }
}
