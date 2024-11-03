using Audio.Conversion.Chunks;
using Audio.Conversion.Utils;
using System.Buffers.Binary;
using System.Numerics;

namespace Audio.Conversion.Codecs;
public class Vorbis : RIFFStream
{
    private const string Codebook = "packed_codebooks_aoTuV_603.bin";

    public override string Extension => ".ogg";

    public Vorbis(Stream stream, RIFFHeader header) : base(stream, header) { }

    public override void CopyTo(Stream destination, int bufferSize = 81920)
    {
        using OGGStream oggStream = new(destination);

        if (Header.GetChunk(out VORB? vorb) && Header.GetChunk(out DATA? data))
        {
            Position = data.Header.Offset;

            if (vorb.SeekTableSize > vorb.VorbisDataOffset)
            {
                Logger.Warning("Seek table is corrupted !!");
                return;
            }

            Span<byte> buffer = stackalloc byte[4]; 

            List<VorbisSeekEntry> seekTable = [];
            for (int i = 0; i < vorb.SeekTableSize / 4; i++)
            {
                ReadExactly(buffer[..2]);
                uint frameOffset = BinaryPrimitives.ReadUInt16LittleEndian(buffer) + (seekTable.LastOrDefault()?.FrameOffset ?? 0);

                ReadExactly(buffer[..2]);
                uint fileOffset = BinaryPrimitives.ReadUInt16LittleEndian(buffer) + (seekTable.LastOrDefault()?.FileOffset ?? 0);

                seekTable.Add(new(frameOffset, fileOffset));
            }

            if (TryGenerateHeader(oggStream, out bool[] blockFlags, out int modeBitCount))
            {
                Position = data.Header.Offset + vorb.VorbisDataOffset;

                int prevBlockSize = 0;
                bool prevBlockFlag = false;
                bool needMod = blockFlags.Length > 0 && modeBitCount > 0;
                using BitStream bitStream = new(_baseStream, false, true);
                while (Position < data.Header.Offset + data.Header.Length)
                {
                    VorbisPacket packet = new(_baseStream, Position);

                    if (Position + packet.Size > data.Header.Offset + data.Header.Length)
                    {
                        Logger.Warning("Audio packet header truncated !!");
                        return;
                    }

                    Position = packet.Offset;

                    int mode = 0;
                    int size = packet.Size;
                    if (needMod)
                    {
                        bitStream.Position = packet.Offset * 8;

                        oggStream.Write(1); // packetType

                        uint modeNumber = bitStream.Read(modeBitCount);
                        uint reminder = bitStream.Read(8 - modeBitCount);

                        oggStream.Write(modeBitCount, modeNumber);

                        mode = Convert.ToInt32(blockFlags[modeNumber]);

                        if (blockFlags[modeNumber])
                        {
                            bool nextBlockFlag = false;
                            if (packet.Next + 2 <= data.Header.Offset + data.Header.Length)
                            {
                                VorbisPacket nextPacket = new(_baseStream, packet.Next);
                                if (nextPacket.Size > 0)
                                {
                                    Position = nextPacket.Offset;

                                    bitStream.Position = nextPacket.Offset * 8;

                                    uint nextModeNumber = bitStream.Read(modeBitCount);
                                    nextBlockFlag = blockFlags[nextModeNumber];
                                }
                            }

                            oggStream.Write(1, Convert.ToUInt32(prevBlockFlag));
                            oggStream.Write(1, Convert.ToUInt32(nextBlockFlag));
                            Position = packet.Offset + 1;
                        }

                        prevBlockFlag = blockFlags[modeNumber];

                        oggStream.Write(8 - modeBitCount, reminder);
                        size--;
                    }

                    while (size > 0)
                    {
                        int count = Math.Clamp(size, 0, buffer.Length);
                        int read = Read(buffer[..count]);
                        if (read != count)
                        {
                            Logger.Warning("Audio packet truncated !!");
                            return;
                        }
                    
                        oggStream.Write(read * 8, BinaryPrimitives.ReadUInt32LittleEndian(buffer));
                        
                        buffer.Clear();
                        size -= read;
                    }

                    int blockSize = 1 << vorb.BlockSizes[mode];
                    oggStream.Granule += prevBlockSize == 0 ? 0 : (prevBlockSize + blockSize) / 4;
                    prevBlockSize = blockSize;

                    Position = packet.Next;

                    oggStream.Type &= ~OGGStream.PageType.Continued;
                    if (Position == data.Header.Offset + data.Header.Length)
                    {
                        oggStream.Type |= OGGStream.PageType.Tail;
                    }
                    else
                    {
                        oggStream.Type &= ~OGGStream.PageType.Tail;
                    }

                    oggStream.FlushPacket(true);
                }

                if (Position > data.Header.Offset + data.Header.Length)
                {
                    Logger.Warning("file truncated !!");
                    return;
                }
            }
        }

        oggStream.Close();
        destination.Position = 0;
    }

    private bool TryGenerateHeader(OGGStream oggStream, out bool[] blockFlags, out int modeBitCount)
    {
        blockFlags = [];
        modeBitCount = 0;

        if (Header.GetChunk(out FMT? fmt) && Header.GetChunk(out VORB? vorb))
        {
            WriteHeader(oggStream, VorbisHeaderType.Info);
            oggStream.Write(32); // version
            oggStream.Write(8, fmt.Channels);
            oggStream.Write(32, fmt.SampleRate);
            oggStream.Write(32); // bitrate_max
            oggStream.Write(32, fmt.AverageBitrate * 8);
            oggStream.Write(32); // bitrate_minimum
            oggStream.Write(4, vorb.BlockSizes[0]);
            oggStream.Write(4, vorb.BlockSizes[1]);
            oggStream.Write(1, 1); // framing
            oggStream.FlushPage();

            WriteHeader(oggStream, VorbisHeaderType.Comment);
            WriteString(oggStream, "Converted from Audiokinetic Wwise by ww2ogg 0.24");
            if (false) // TODO: loops
            {
                oggStream.Write(32, 2);
                WriteString(oggStream, $"LoopStart={vorb.LoopInfo.LoopBeginExtra}");
                WriteString(oggStream, $"LoopEnd={vorb.LoopInfo.LoopEndExtra}");
            }
            else
            {
                oggStream.Write(32);
            }

            oggStream.Write(1, 1); // framing
            oggStream.FlushPacket();

            if (Header.GetChunk(out DATA? data))
            {
                WriteHeader(oggStream, VorbisHeaderType.Books);
                VorbisPacket setupPacket = new(_baseStream, data.Header.Offset + vorb.SeekTableSize);

                using BitStream bitStream = new(_baseStream, leaveOpen: true);
                bitStream.Position = setupPacket.Offset * 8;

                uint prevCodebookCount = bitStream.Read(8);
                oggStream.Write(8, prevCodebookCount);
                uint codebookCount = prevCodebookCount + 1;

                if (Conversion.Codebook.TryOpen(Codebook, out Codebook? codebook))
                {
                    try
                    {
                        for (int i = 0; i < codebookCount; i++)
                        {
                            int codebookID = (int)bitStream.Read(10);
                            codebook.RebuildPCB(codebookID, oggStream);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error($"Error while generating OGG header: {e}");
                        return false;
                    }
                }

                oggStream.Write(6); // timeCountLess1
                oggStream.Write(16); // ignoreTimeValue

                uint floorCountLess1 = bitStream.Read(6);
                oggStream.Write(6, floorCountLess1);

                uint floorCount = floorCountLess1 + 1;
                for (int i = 0; i < floorCount; i++)
                {
                    oggStream.Write(16, 1); // floorType

                    uint floorPartitionsCount = bitStream.Read(5);
                    oggStream.Write(5, floorPartitionsCount);

                    uint[] floorPartitions = new uint[floorPartitionsCount];
                    for (int j = 0; j < floorPartitionsCount; j++)
                    {
                        uint floorPartition = bitStream.Read(4);
                        oggStream.Write(4, floorPartition);

                        floorPartitions[j] = floorPartition;
                    }

                    int maxPartition = (int)floorPartitions.DefaultIfEmpty().Max();
                    uint[] floorDimensions = new uint[maxPartition + 1];
                    for (int j = 0; j <= maxPartition; j++)
                    {
                        uint partitionDimensionLess1 = bitStream.Read(3);
                        oggStream.Write(3, partitionDimensionLess1);

                        floorDimensions[j] = partitionDimensionLess1 + 1;

                        uint subPartitions = bitStream.Read(2);
                        oggStream.Write(2, subPartitions);

                        if (subPartitions != 0)
                        {
                            uint masterBook = bitStream.Read(8);
                            oggStream.Write(8, masterBook);

                            if (masterBook >= codebookCount)
                            {
                                Logger.Warning($"Master book {masterBook} is out of range {codebookCount}");
                                return false;
                            }
                        }

                        for (int k = 0; k < 1 << (int)subPartitions; k++)
                        {
                            uint SubPartitionBookPlus1 = bitStream.Read(8);
                            oggStream.Write(8, SubPartitionBookPlus1);

                            uint subPartitionBook = SubPartitionBookPlus1 - 1;
                            if (subPartitionBook >= 0 && subPartitions >= codebookCount)
                            {
                                Logger.Warning($"Sub partition {subPartitions} is out of range {codebookCount}");
                                return false;
                            }
                        }
                    }

                    oggStream.Write(2, bitStream.Read(2)); // floorMultiplierLess1

                    uint rangeBitCount = bitStream.Read(4);
                    oggStream.Write(4, rangeBitCount);

                    for (int j = 0; j < floorPartitionsCount; j++)
                    {
                        uint currentPartition = floorPartitions[j];
                        for (int k = 0; k < floorDimensions[currentPartition]; k++)
                        {
                            oggStream.Write((int)rangeBitCount, bitStream.Read((int)rangeBitCount)); // X
                        }
                    }
                }

                uint residueCountLess1 = bitStream.Read(6);
                oggStream.Write(6, residueCountLess1);

                uint residueCount = residueCountLess1 + 1;
                for (int i = 0; i < residueCount; i++)
                {
                    uint residueType = bitStream.Read(2);
                    oggStream.Write(16, residueType);

                    if (residueType > 2)
                    {
                        Logger.Warning($"Invalid residue type {residueType}");
                        return false;
                    }

                    oggStream.Write(24, bitStream.Read(24)); // residueBegin
                    oggStream.Write(24, bitStream.Read(24)); // residueEnd
                    oggStream.Write(24, bitStream.Read(24)); // residuePartitionSizeLess1

                    uint residueClassificationCountLess1 = bitStream.Read(6);
                    uint residueClassbook = bitStream.Read(8);

                    oggStream.Write(6, residueClassificationCountLess1);
                    oggStream.Write(8, residueClassbook);

                    if (residueClassbook >= codebookCount)
                    {
                        Logger.Warning($"Residue classbook {residueClassbook} is out of range {codebookCount}");
                        return false;
                    }

                    uint residueClassificationCount = residueClassificationCountLess1 + 1;
                    uint[] residueCascade = new uint[residueClassificationCount];
                    for (int j = 0; j < residueClassificationCount; j++)
                    {
                        uint highBits = 0;

                        uint lowBits = bitStream.Read(3);
                        oggStream.Write(3, lowBits);

                        uint bitFlag = bitStream.Read(1);
                        oggStream.Write(1, bitFlag);

                        if (bitFlag != 0)
                        {
                            highBits = bitStream.Read(5);
                            oggStream.Write(5, highBits);
                        }

                        residueCascade[j] = highBits * 8 + lowBits;
                    }

                    for (int j = 0; j < residueClassificationCount; j++)
                    {
                        for (int k = 0; k < 8; k++)
                        {
                            if ((residueCascade[j] & 1 << k) != 0)
                            {
                                uint residueBook = bitStream.Read(8);
                                oggStream.Write(8, residueBook);

                                if (residueBook >= codebookCount)
                                {
                                    Logger.Warning($"Residue classbook {residueBook} is out of range {codebookCount}");
                                    return false;
                                }
                            }
                        }
                    }
                }

                uint mapCountLess1 = bitStream.Read(6);
                oggStream.Write(6, mapCountLess1);

                uint mapCount = mapCountLess1 + 1;
                for (int i = 0; i < mapCount; i++)
                {
                    oggStream.Write(16); // mapType

                    uint subMapFlag = bitStream.Read(1);
                    oggStream.Write(1, subMapFlag);

                    uint subMapCount = 1;
                    if (subMapFlag != 0)
                    {
                        uint subMapCountLess1 = bitStream.Read(4);
                        oggStream.Write(4, subMapCountLess1);

                        subMapCount = subMapCountLess1 + 1;
                    }

                    uint squarePolarFlag = bitStream.Read(1);
                    oggStream.Write(1, squarePolarFlag);

                    if (squarePolarFlag != 0)
                    {
                        uint couplingStepsCountLess1 = bitStream.Read(8);
                        oggStream.Write(8, couplingStepsCountLess1);

                        int couplingBitCount = fmt.Channels - 1;
                        if (couplingBitCount > 0)
                        {
                            couplingBitCount = BitOperations.Log2((uint)fmt.Channels - 1) + 1;
                        }

                        uint couplingStepsCount = couplingStepsCountLess1 + 1;
                        for (int j = 0; j < couplingStepsCount; j++)
                        {
                            uint magnitude = bitStream.Read(couplingBitCount);
                            uint angle = bitStream.Read(couplingBitCount);

                            oggStream.Write(couplingBitCount, magnitude);
                            oggStream.Write(couplingBitCount, angle);

                            if (angle == magnitude || magnitude >= fmt.Channels || angle >= fmt.Channels)
                            {
                                Logger.Warning($"Invalid coupling");
                                return false;
                            }
                        }
                    }

                    uint mapReserved = bitStream.Read(2);
                    oggStream.Write(2, mapReserved);

                    if (mapReserved != 0)
                    {
                        Logger.Warning($"Expected map reserved to be 0, got {mapReserved} !!");
                        return false;
                    }

                    if (subMapCount > 1)
                    {
                        for (int j = 0; j < fmt.Channels; j++)
                        {
                            uint mapMux = bitStream.Read(4);
                            oggStream.Write(4, mapMux);

                            if (mapMux >= subMapCount)
                            {
                                Logger.Warning($"Map mux {mapMux} is out of range {subMapCount}");
                                return false;
                            }
                        }
                    }

                    for (int j = 0; j < subMapCount; j++)
                    {
                        oggStream.Write(8, bitStream.Read(8)); // timeConfig

                        uint floorNumber = bitStream.Read(8);
                        oggStream.Write(8, floorNumber);

                        if (floorNumber >= floorCount)
                        {
                            Logger.Warning($"Floor number {floorNumber} is out of range {floorCount}");
                            return false;
                        }

                        uint residueNumber = bitStream.Read(8);
                        oggStream.Write(8, residueNumber);

                        if (residueNumber >= residueCount)
                        {
                            Logger.Warning($"Residue number {residueNumber} is out of range {residueCount}");
                            return false;
                        }
                    }
                }

                uint modeCountLess1 = bitStream.Read(6);
                oggStream.Write(6, modeCountLess1);

                modeBitCount = (int)modeCountLess1;
                if (modeBitCount > 0)
                {
                    modeBitCount = BitOperations.Log2(modeCountLess1) + 1;
                }

                uint modeCount = modeCountLess1 + 1;
                blockFlags = new bool[modeCount];
                for (int i = 0; i < modeCount; i++)
                {
                    uint blockFlag = bitStream.Read(1);
                    oggStream.Write(1, blockFlag);

                    blockFlags[i] = blockFlag != 0;

                    oggStream.Write(16); // windowType
                    oggStream.Write(16); // transformType

                    uint mapNumber = bitStream.Read(8);
                    oggStream.Write(8, mapNumber);

                    if (mapNumber >= mapCount)
                    {
                        Logger.Warning($"Map number {mapNumber} is out of range {mapCount}");
                        return false;
                    }
                }

                oggStream.Write(1, 1); // framing
                oggStream.FlushPacket();
                oggStream.FlushPage();

                long read = bitStream.Position - setupPacket.Offset * 8;

                read += 7;
                read &= ~7;

                if (read != setupPacket.Size * 8)
                {
                    Logger.Warning($"Expected {setupPacket.Size * 8} bits, got {bitStream.Position}");
                    return false;
                }

                if (data.Header.Offset + vorb.VorbisDataOffset != setupPacket.Next)
                {
                    Logger.Warning("No audio packets found after setup packet !!");
                    return false;
                }

                return true;
            }
        }

        return false;
    }

    private static void WriteHeader(OGGStream oggStream, VorbisHeaderType type)
    {
        oggStream.Write(8, (uint)type);

        foreach (byte c in "vorbis"u8)
        {
            oggStream.Write(8, c);
        }
    }

    private static void WriteString(OGGStream oggStream, ReadOnlySpan<char> value)
    {
        oggStream.Write(32, (uint)value.Length);

        foreach (char c in value)
        {
            oggStream.Write(8, c);
        }
    }
}
