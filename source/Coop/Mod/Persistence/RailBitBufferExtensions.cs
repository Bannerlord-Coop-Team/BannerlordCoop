using RailgunNet.System.Encoding;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Persistence
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
        public static void WriteMBGUID(this RailBitBuffer buffer, MBGUID guid)
        {
            buffer.WriteUInt(guid.InternalValue);
        }

        [Decoder]
        public static MBGUID ReadMBGUID(this RailBitBuffer buffer)
        {
            return new MBGUID(buffer.ReadUInt());
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
