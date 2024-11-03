using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Audio.Entries;
public abstract record Entry : IBankReadable
{
    public uint Offset { get; set; }
    public int Size { get; set; }
    public int FolderIndex { get; set; }
    public EntryType Type { get; set; }
    [JsonIgnore]
    public Folder? Folder { get; set; }
    [JsonIgnore]
    public string? Source { get; set; }

    public virtual ulong ID { get; set; }
    public virtual string? Name { get; set; }
    [JsonIgnore]
    public virtual string? FolderName => Folder?.Name ?? "None";
    public virtual string? Location => $"{FolderName}/{Name}";
    
    public Entry(EntryType type)
    {
        Type = type;
    }

    public virtual void Read(BankReader reader)
    {
        uint offsetMultiplier = reader.ReadUInt32();
        Size = reader.ReadInt32();
        Offset = reader.ReadUInt32() * offsetMultiplier;
        FolderIndex = reader.ReadInt32();
        Source = reader.Source;
    }
    public virtual bool TryOpen([NotNullWhen(true)] out Stream? stream)
    {
        if (Size > 0 && File.Exists(Source))
        {
            try
            {
                stream = File.OpenRead(Source);
                stream.Position = Offset;
                return true;
            }
            catch (Exception e)
            {
                Logger.Error($"Error while opening entry {Location}, {e}");
            }
        }

        stream = null;
        return false;
    }
    public virtual bool TryWrite(Stream outStream)
    {
        if (outStream.CanWrite && TryOpen(out Stream? inputStream))
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(Size);
            Span<byte> bufferSpan = buffer.AsSpan(0, Size);

            try
            {
                inputStream.ReadExactly(bufferSpan);

                outStream.Write(bufferSpan);
                outStream.Position = 0;

                return true;
            }
            catch (Exception e)
            {
                Logger.Error($"Error while writing entry {Location} to stream, {e}");
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        return false;
    }
}