using Coop.Mod.Persistence;
using RailgunNet.System.Encoding;
using RailgunNet.System.Types;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.DebugUtil
{
    public class MoveToEvent
    {
        public CampaignTime time;
        public EntityId entityId;
        public MobileParty party;
        public MovementData movement;
        public bool passed = false;

        public override bool Equals(object obj)
        {
            return obj is MoveToEvent other &&
                this.entityId == other.entityId &&
                this.party?.Id == other.party?.Id &&
                this.movement.NearlyEquals(other.movement);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public static class MoveToEventSerializer
    {
        [Encoder]
        public static void WriteMoveToEvent(this RailBitBuffer buffer, MoveToEvent moveTo)
        {
            buffer.WriteCampaignTime(moveTo.time);
            buffer.WriteEntityId(moveTo.entityId);
            buffer.WriteMBGUID(moveTo.party.Id);
            buffer.WriteMovementData(moveTo.movement);
        }

        [Decoder]
        public static MoveToEvent ReadMoveToEvent(this RailBitBuffer buffer)
        {
            return new MoveToEvent
            {
                time = buffer.ReadCampaignTime(),
                entityId = buffer.ReadEntityId(),
                party = (MobileParty)MBObjectManager.Instance.GetObject(buffer.ReadMBGUID()),
                movement = buffer.ReadMovementData(),
            };
        }
    }
}
