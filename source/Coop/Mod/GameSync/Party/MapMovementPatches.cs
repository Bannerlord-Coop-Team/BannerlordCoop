using Coop.Mod.Persistence.Party;
using Sync.Call;
using Sync.Value;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace Coop.Mod.GameSync.Party
{
    internal class MapMovementPatches
    {
        /// <summary>
        ///     Field access for the position on the campaign map.
        /// </summary>
        public FieldAccess<MobileParty, Vec2> MapPosition { get; set; }
        public PatchedInvokable MapPositionSetter { get; set; }
        public PatchedInvokable TargetPositionSetter { get; set; }
        public PatchedInvokable TargetPartySetter { get; set; }
        public PatchedInvokable TargetSettlementSetter { get; set; }
        public PatchedInvokable DefaultBehaviourSetter { get; set; }

        /// <summary>
        ///     Field access group for all movement related data.
        /// </summary>
        public FieldAccessGroup<MobileParty, MovementData> MovementOrderGroup { get; set; }
    }
}