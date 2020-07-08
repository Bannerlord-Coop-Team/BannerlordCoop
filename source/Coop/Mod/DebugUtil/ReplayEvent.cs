using Coop.Mod.Persistence;
using RailgunNet.System.Encoding;
using RailgunNet.System.Types;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.DebugUtil
{
    /// <summary>
    /// Used for store info about movements of parties on campaign map into file which will be used
    /// for playback recorded time interval.
    /// </summary>
    public class ReplayEvent
    {
        public CampaignTime time;
        public EntityId entityId;
        public MobileParty party;
        public MovementData movement;
        public bool applied = false;

        public override bool Equals(object obj)
        {
            return obj is ReplayEvent other &&
                this.entityId == other.entityId &&
                this.party?.Id == other.party?.Id &&
                this.movement.NearlyEquals(other.movement);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    /// <summary>
    /// Serializer for object ReplayEvent.
    /// </summary>
    public static class ReplayEventSerializer
    {
        [Encoder]
        public static void WriteReplayEvent(this RailBitBuffer buffer, ReplayEvent replay)
        {
            buffer.WriteCampaignTime(replay.time);
            buffer.WriteEntityId(replay.entityId);
            buffer.WriteMBGUID(replay.party.Id);
            buffer.WriteMovementData(replay.movement);
        }

        [Decoder]
        public static ReplayEvent ReadReplayEvent(this RailBitBuffer buffer)
        {
            return new ReplayEvent
            {
                time = buffer.ReadCampaignTime(),
                entityId = buffer.ReadEntityId(),
                party = (MobileParty)MBObjectManager.Instance.GetObject(buffer.ReadMBGUID()),
                movement = buffer.ReadMovementData(),
            };
        }
    }
}
