using Audio.Chunks;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Audio.Entries;
public record Bank : Entry
{
    private FNVID<uint> _id;

    [JsonIgnore]
    public BKHD? BKHD { get; set; }
    public Dictionary<string, Chunk> Chunks { get; set; } = [];

    public override ulong ID => _id.Value;
    public override string? Name => _id.ToString();
    public override string? Location => $"{base.Location}.bnk";

    [JsonIgnore]
    public IEnumerable<Entry> Entries
    {
        get
        {
            yield return this;

            if (GetChunk(out DIDX? didx) == true)
            {
                foreach (EmbeddedSound embeddedSound in didx.EmbeddedSounds)
                {
                    embeddedSound.Bank = this;
                    yield return embeddedSound;
                }
            }
        }
    }

    public Bank() : base(EntryType.Bank)
    {
        _id = new();
    }

    public Bank(BKHD bkhd, string source) : base(EntryType.Bank)
    {
        BKHD = bkhd;

        _id = bkhd.ID;
        Offset = 0;
        if (File.Exists(source))
        {
            Source = source;
            Size = (int)new FileInfo(source).Length;
        }
    }

    public override void Read(BankReader reader)
    {
        _id = reader.ReadUInt32();

        base.Read(reader);
    }
    public void Parse(BankReader reader)
    {
        reader.BaseStream.Position = Offset;
        reader.Root = BKHD;

        while (reader.BaseStream.Position < Offset + Size)
        {
            if (Chunk.TryParse(reader, out Chunk? chunk))
            {
                if (BKHD == null && chunk is BKHD bkhd)
                {
                    BKHD = bkhd;
                    reader.Root = bkhd;
                }

                Chunks.Add(chunk.Header.Signature, chunk);
            }
        }
    }
    public bool GetChunk<T>([NotNullWhen(true)] out T? chunk) where T : Chunk
    {
        string signature = typeof(T) switch
        {
            Type _ when typeof(T) == typeof(AKPK) => AKPK.Signature,
            Type _ when typeof(T) == typeof(BKHD) => BKHD.Signature,
            Type _ when typeof(T) == typeof(HIRC) => HIRC.Signature,
            Type _ when typeof(T) == typeof(STMG) => STMG.Signature,
            Type _ when typeof(T) == typeof(STID) => STID.Signature,
            Type _ when typeof(T) == typeof(DIDX) => DIDX.Signature,
            Type _ when typeof(T) == typeof(DATA) => DATA.Signature,
            _ => throw new NotImplementedException(),
        };

        if (Chunks.TryGetValue(signature, out Chunk? chk))
        {
            chunk = (T)chk;
            return true;
        }

        chunk = null;
        return false;
    }
}
