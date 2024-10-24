using Audio.Conversion.Utils;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Audio.Conversion;
public class WWiseCodebook
{
    private const uint Signature = 0x564342;

    private readonly byte[] _codebookData;
    private readonly int[] _codebookOffsets;
    private readonly int _codebookSize;

    public WWiseCodebook(Stream stream)
    {
        stream.Seek(-4, SeekOrigin.End);

        Span<byte> buffer = stackalloc byte[4];
        stream.ReadExactly(buffer);
        _codebookSize = BinaryPrimitives.ReadInt32LittleEndian(buffer);

        _codebookData = new byte[_codebookSize];
        _codebookOffsets = new int[(int)(stream.Length - _codebookSize) / 4];

        stream.Position = 0;
        stream.ReadExactly(_codebookData.AsSpan());
        stream.ReadExactly(MemoryMarshal.AsBytes(_codebookOffsets.AsSpan()));
    }

    public Span<byte> GetCodebook(int index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _codebookOffsets.Length);

        int offset = _codebookOffsets[index];
        int size = _codebookData.Length - offset;
        if (index < _codebookOffsets.Length - 1)
        {
            size = _codebookOffsets[index + 1] - offset;
        }

        return _codebookData.AsSpan(offset, size);
    }

    public void RebuildPCB(int index, OGGStream oggStream)
    {
        Span<byte> codebook = GetCodebook(index);
        using MemoryStream ms = new(codebook.ToArray());
        using BitStream pcbStream = new(ms);

        RebuildStream(pcbStream, oggStream);

        if (pcbStream.Position < pcbStream.Length - 8)
        {
            throw new InvalidOperationException("Stream not entirely consumed !!");
        }
    }

    public static void RebuildStream(BitStream bitStream, OGGStream oggStream)
    {
        uint dimensions = bitStream.Read(4);
        uint entries = bitStream.Read(14);

        oggStream.Write(24, Signature);
        oggStream.Write(16, dimensions);
        oggStream.Write(24, entries);

        uint ordered = bitStream.Read(1);
        oggStream.Write(1, ordered);

        if (ordered != 0)
        {
            oggStream.Write(5, bitStream.Read(5)); // InitialLength

            uint currentEntry = 0;
            while (currentEntry < entries)
            {
                int remainingEntry = (int)(entries - currentEntry);
                if (remainingEntry > 0)
                {
                    remainingEntry = BitOperations.Log2(entries - currentEntry) + 1;
                }

                uint count = bitStream.Read(remainingEntry);
                oggStream.Write(remainingEntry, count);
                currentEntry += count;
            }

            if (currentEntry > entries)
            {
                throw new InvalidOperationException("Current entry is out of range.");
            }
        }
        else
        {
            uint codewordSizeBitcount = bitStream.Read(3);
            uint sparse = bitStream.Read(1);

            if (codewordSizeBitcount <= 0 || codewordSizeBitcount > 5)
            {
                throw new InvalidOperationException("Invalid Codeword sizes count.");
            }

            oggStream.Write(1, sparse);

            for (uint i = 0; i < entries; i++)
            {
                uint present = 1;

                if (sparse != 0)
                {
                    present = bitStream.Read(1);
                    oggStream.Write(1, present);
                }

                if (present != 0)
                {
                    uint codewordSize = bitStream.Read((int)codewordSizeBitcount);
                    oggStream.Write(5, codewordSize);
                }
            }
        }

        uint lookupType = bitStream.Read(1);
        oggStream.Write(4, lookupType);
        if (lookupType == 1)
        {
            uint min = bitStream.Read(32);
            uint max = bitStream.Read(32);
            uint bitCount = bitStream.Read(4);
            uint sequanceFlag = bitStream.Read(1);

            oggStream.Write(32, min);
            oggStream.Write(32, max);
            oggStream.Write(4, bitCount);
            oggStream.Write(1, sequanceFlag);

            int count = (int)bitCount + 1;
            uint qCount = QuantCount(entries, dimensions);
            for (uint i = 0; i < qCount; i++)
            {
                oggStream.Write(count, bitStream.Read(count));
            }
        }
        else if (lookupType != 0)
        {
            throw new InvalidOperationException($"Unknown lookup type {lookupType}");
        }
    }

    public static void Copy(BitStream bitStream, OGGStream oggStream)
    {
        uint signature = bitStream.Read(24);
        uint dimensions = bitStream.Read(16);
        uint entries = bitStream.Read(24);

        if (signature != Signature)
        {
            throw new InvalidDataException($"Expected {Signature}, got {signature} instead !!");
        }

        oggStream.Write(24, signature);
        oggStream.Write(16, dimensions);
        oggStream.Write(24, entries);

        uint ordered = bitStream.Read(1);
        oggStream.Write(1, ordered);

        if (ordered != 0)
        {
            oggStream.Write(5, bitStream.Read(5)); // InitialLength

            uint currentEntry = 0;
            while (currentEntry < entries)
            {
                int remainingEntry = (int)(entries - currentEntry);
                if (remainingEntry > 0)
                {
                    remainingEntry = BitOperations.Log2(entries - currentEntry) + 1;
                }

                uint count = bitStream.Read(remainingEntry);
                oggStream.Write(remainingEntry);
                currentEntry += count;
            }

            if (currentEntry > entries)
            {
                throw new InvalidOperationException("Current entry is out of range.");
            }
        }
        else
        {
            uint sparse = bitStream.Read(1);
            oggStream.Write(1, sparse);

            for (uint i = 0; i < entries; i++)
            {
                uint present = 1;

                if (sparse != 0)
                {
                    present = bitStream.Read(1);
                    oggStream.Write(1, present);
                }

                if (present != 0)
                {
                    oggStream.Write(5, bitStream.Read(5)); // codewordLength
                }
            }
        }

        uint lookupType = bitStream.Read(4);
        oggStream.Write(4, lookupType);
        if (lookupType == 1)
        {
            uint min = bitStream.Read(32);
            uint max = bitStream.Read(32);
            uint bitCount = bitStream.Read(4);
            uint sequanceFlag = bitStream.Read(1);

            oggStream.Write(32, min);
            oggStream.Write(32, max);
            oggStream.Write(4, bitCount);
            oggStream.Write(1, sequanceFlag);

            int count = (int)bitCount + 1;
            uint qCount = QuantCount(entries, dimensions);
            for (uint i = 0; i < qCount; i++)
            {
                oggStream.Write(count, bitStream.Read(count));
            }
        }
        else if (lookupType != 0)
        {
            throw new InvalidOperationException($"Unknown lookup type {lookupType}");
        }
    }

    private static uint QuantCount(uint entries, uint dimensions)
    {
        int bits = (int)entries;
        if (bits > 0)
        {
            bits = BitOperations.Log2(entries) + 1;
        }

        uint acc, nextAcc, vals = entries >> (int)((bits - 1) * (dimensions - 1) / dimensions);
        while (true)
        {
            acc = 1;
            nextAcc = 1;

            for (int i = 0; i < dimensions; i++)
            {
                acc *= vals;
                nextAcc *= vals + 1;
            }

            if (acc <= entries && nextAcc > entries)
            {
                return vals;
            }
            else
            {
                if (acc > entries)
                {
                    vals--;
                }
                else
                {
                    vals++;
                }
            }
        }
    }

    public static bool TryOpen(string path, [NotNullWhen(true)] out WWiseCodebook? codebook)
    {
        try
        {
            using Stream stream = File.OpenRead(path);
            codebook = new WWiseCodebook(stream);
            return true;
        }
        catch (Exception) { }

        codebook = null;
        return false;
    }
}