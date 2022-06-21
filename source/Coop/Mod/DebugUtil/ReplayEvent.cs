using Coop.Mod.Persistence;
using RailgunNet.System.Encoding;
using RailgunNet.System.Types;
using Coop.Mod.Persistence.Party;
using RemoteAction;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;
using Common;
using TaleWorlds.CampaignSystem.Party;

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
        public MapVec2 position;
        public bool applied = false;

        public override bool Equals(object obj)
        {
            return obj is ReplayEvent other &&
                this.entityId == other.entityId &&
                this.party?.Id == other.party?.Id &&
                this.position.Equals(other.position);
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
            buffer.WriteGUID(CoopObjectManager.GetGuid(replay.party));
            buffer.WriteMapVec2(replay.position);
        }

        [Decoder]
        public static ReplayEvent ReadReplayEvent(this RailBitBuffer buffer)
        {
            return new ReplayEvent
            {
                time = buffer.ReadCampaignTime(),
                entityId = buffer.ReadEntityId(),
                party = (MobileParty)CoopObjectManager.GetObject(buffer.ReadGUID()),
                position = buffer.ReadMapVec2()
            };
        }
    }
}
