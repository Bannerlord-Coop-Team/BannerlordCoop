using RailgunNet.System.Encoding;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Persistence
{
    public static class RailBitBufferExtensions
    {
        [Encoder]
        public static void Encode(this RailBitBuffer buffer, CampaignTimeControlMode mode)
        {
            buffer.WriteByte((byte) mode);
        }

        [Decoder]
        public static CampaignTimeControlMode DecodeTimeControlMode(this RailBitBuffer buffer)
        {
            return (CampaignTimeControlMode) buffer.ReadByte();
        }
    }
}
