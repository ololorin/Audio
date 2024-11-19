using Audio.Conversion.Chunks;
using System.Buffers;
using System.Text;

namespace Audio.Conversion.Codecs;
public class PTADPCM : RIFFStream
{
    private static readonly short[] SampleSteps = [-28, -20, -14, -10, -7, -5, -3, -1, 1, 3, 5, 7, 10, 14, 20, 28];
    private static readonly int[] SampleIndices = [2, 2, 1, 1, 0, 0, 0, -1, -1, 0, 0, 0, 1, 1, 2, 2];
    private static readonly int MaxSampleIndex = 12;

    public PTADPCM(Stream stream, RIFFHeader header) : base(stream, header) { }

    public override void CopyTo(Stream destination, int bufferSize = 81920)
    {
        if (Header.GetChunk(out FMT? fmt))
        {
            if (fmt.Channels <= 0 || fmt.BitsPerSample != 4 || fmt.BlockSize != 0x24 * fmt.Channels && fmt.BlockSize != 0x104 * fmt.Channels)
            {
                throw new InvalidOperationException("Invalid PTADPCM format !!");
            }

            if (Header.GetChunk(out DATA? data))
            {
                int interleaveBlockSize = fmt.BlockSize / fmt.Channels;
                if (interleaveBlockSize < 6)
                {
                    throw new InvalidOperationException("Invalid PTADPCM block size !!");
                }

                int samplePerFrame = 2 + (interleaveBlockSize - 5) * 2;
                long numSamples = data.Header.Length / (fmt.Channels * interleaveBlockSize) * samplePerFrame;

                short[] buffer = ArrayPool<short>.Shared.Rent((int)(fmt.Channels * numSamples));
                try
                {
                    using BinaryReader reader = new(_baseStream, Encoding.UTF8, true);
                    reader.BaseStream.Position = data.Header.Offset;

                    for (int i = 0; i < numSamples; i += samplePerFrame)
                    {
                        for (int j = 0; j < fmt.Channels; j++)
                        {
                            short hist2 = reader.ReadInt16();
                            short hist1 = reader.ReadInt16();
                            int index = Math.Min(reader.ReadByte(), MaxSampleIndex);

                            buffer[j * numSamples + i] = hist2;
                            buffer[j * numSamples + i + 1] = hist1;

                            byte b = 0;
                            for (int k = 2; k < samplePerFrame; k++)
                            {
                                if (k % 2 == 0)
                                {
                                    b = reader.ReadByte();
                                }

                                byte nipple = (byte)(b >> k % 2 * 4 & 0xF);

                                int step = (SampleSteps[nipple] << index) / 2;
                                index += SampleIndices[nipple];

                                int sample = Math.Clamp(step + 2 * hist1 - hist2, short.MinValue, short.MaxValue);
                                index = Math.Clamp(index, 0, MaxSampleIndex - 1);

                                buffer[j * numSamples + i + k] = (short)sample;

                                hist2 = hist1;
                                hist1 = (short)sample;
                            }
                        }
                    }

                    int headerSize = 0x2C;
                    long dataSize = numSamples * fmt.Channels * 2;
                    long fileSize = headerSize + dataSize;

                    HeaderInfo dataHeader = new()
                    {
                        Signature = DATA.Signature,
                        Length = (uint)dataSize,
                        Offset = headerSize,
                    };

                    HeaderInfo fmtHeader = new()
                    {
                        Signature = FMT.Signature,
                        Length = 0x10,
                        Offset = 0x14,
                    };

                    FMT waveFmt = new(fmtHeader)
                    {
                        Format = WAVEFormat.PCM,
                        Channels = fmt.Channels,
                        SampleRate = fmt.SampleRate,
                        AverageBitrate = fmt.SampleRate * fmt.Channels * 2,
                        BlockSize = (ushort)(fmt.Channels * 2),
                        BitsPerSample = 0x10
                    };

                    using BinaryWriter writer = new(destination, Encoding.UTF8, true);

                    writer.Write(Encoding.UTF8.GetBytes(RIFFHeader.Signature));
                    writer.Write((uint)(fileSize - 8));
                    writer.Write(Encoding.UTF8.GetBytes(WAVEChunk.Signature));
                    writer.Write(Encoding.UTF8.GetBytes(fmtHeader.Signature));
                    writer.Write(fmtHeader.Length);
                    writer.Write((ushort)waveFmt.Format);
                    writer.Write(waveFmt.Channels);
                    writer.Write(waveFmt.SampleRate);
                    writer.Write(waveFmt.AverageBitrate);
                    writer.Write(waveFmt.BlockSize);
                    writer.Write(waveFmt.BitsPerSample);
                    writer.Write(Encoding.UTF8.GetBytes(dataHeader.Signature));
                    writer.Write(dataHeader.Length);

                    for (int i = 0; i < numSamples; i++)
                    {
                        for (int j = 0; j < waveFmt.Channels; j++)
                        {
                            writer.Write(buffer[j * numSamples + i]);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Warning($"Error while converting PTADPCM RIFF file, {e}");
                    return;
                }
                finally
                {
                    ArrayPool<short>.Shared.Return(buffer);
                }
            }
        }

        destination.Position = 0;
    }
}
