using RailgunNet.System.Encoding;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace RemoteAction
{
    /// <summary>
    ///     Extensions for Railgun to encode & decode TaleWorlds classes.
    /// </summary>
    public static class RailBitBufferExtensions
    {
        [Encoder]
        public static void WriteTimeControlMode(
            this RailBitBuffer buffer,
            CampaignTimeControlMode mode)
        {
            buffer.WriteByte((byte) mode);
        }

        [Decoder]
        public static CampaignTimeControlMode ReadTimeControlMode(this RailBitBuffer buffer)
        {
            return (CampaignTimeControlMode) buffer.ReadByte();
        }

        [Encoder]
        public static void WriteGUID(this RailBitBuffer buffer, Guid guid)
        {
            buffer.WriteByteArray(guid.ToByteArray());
        }

        [Decoder]
        public static Guid ReadGUID(this RailBitBuffer buffer)
        {
            return new Guid(buffer.ReadByteArray());
        }

        [Encoder]
        public static void WriteCampaignTime(this RailBitBuffer buffer, CampaignTime time)
        {
            buffer.WriteUInt64((ulong) time.ToMilliseconds);
        }

        [Decoder]
        public static CampaignTime ReadCampaignTime(this RailBitBuffer buffer)
        {
            return CampaignTime.Milliseconds((long) buffer.ReadUInt64());
        }
    }
}