using System;
using System.Runtime.InteropServices;
using TaleWorlds.Library;

namespace Missions.Agents.Packets;

/// <summary>
/// Stores each byte of a numeric field together across a batch, so LZ4 can see shared bits even when the full values differ.
/// </summary>
internal ref struct MovementSnapshotColumns
{
    private readonly Span<byte> bytes;
    private readonly int count;
    private int offset;

    public MovementSnapshotColumns(Span<byte> bytes, int count)
    {
        this.bytes = bytes;
        this.count = count;
        offset = 0;
    }

    public void BeginSnapshot(int index) => offset = index;

    public void WriteByte(byte value)
    {
        bytes[offset] = value;
        offset += count;
    }

    public byte ReadByte()
    {
        byte value = bytes[offset];
        offset += count;
        return value;
    }

    public void WriteUInt32(uint value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + count] = (byte)(value >> 8);
        bytes[offset + (2 * count)] = (byte)(value >> 16);
        bytes[offset + (3 * count)] = (byte)(value >> 24);
        offset += 4 * count;
    }

    public uint ReadUInt32()
    {
        uint value = bytes[offset] |
                     ((uint)bytes[offset + count] << 8) |
                     ((uint)bytes[offset + (2 * count)] << 16) |
                     ((uint)bytes[offset + (3 * count)] << 24);
        offset += 4 * count;
        return value;
    }

    // Signed columns preserve the same four bytes as the unsigned representation.
    public void WriteInt32(int value) => WriteUInt32(unchecked((uint)value));
    public int ReadInt32() => unchecked((int)ReadUInt32());

    public void WriteUInt64(ulong value)
    {
        WriteUInt32((uint)value);
        WriteUInt32((uint)(value >> 32));
    }

    public ulong ReadUInt64()
    {
        uint low = ReadUInt32();
        uint high = ReadUInt32();
        return low | ((ulong)high << 32);
    }

    public void WriteSingle(float value) => WriteUInt32(new FloatBits { Value = value }.Bits);
    public float ReadSingle() => new FloatBits { Bits = ReadUInt32() }.Value;

    public void WriteVec2(Vec2 value)
    {
        WriteSingle(value.X);
        WriteSingle(value.Y);
    }

    public void WriteVec3(Vec3 value)
    {
        WriteSingle(value.X);
        WriteSingle(value.Y);
        WriteSingle(value.Z);
    }

    public Vec2 ReadVec2() => new Vec2(ReadSingle(), ReadSingle());
    public Vec3 ReadVec3() => new Vec3(ReadSingle(), ReadSingle(), ReadSingle());

    [StructLayout(LayoutKind.Explicit)]
    private struct FloatBits
    {
        [FieldOffset(0)] public float Value;
        [FieldOffset(0)] public uint Bits;
    }
}
