using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace Audio.Chunks.Types.HIRC;

[StructLayout(LayoutKind.Explicit, Size = 12)]
public struct DecisionTreeNode
{
    [JsonInclude]
    [FieldOffset(0)]
    public FNVID<uint> Key;
    [JsonInclude]
    [FieldOffset(4)]
    public FNVID<uint> NodeID;
    [JsonInclude]
    [FieldOffset(4)]
    public short NodeIndex;
    [JsonInclude]
    [FieldOffset(6)]
    public short NodeCount;
    [JsonInclude]
    [FieldOffset(8)]
    public ushort Weight;
    [JsonInclude]
    [FieldOffset(10)]
    public ushort Probability;
}