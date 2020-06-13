using System.Linq;
using Coop.Mod.Patch;
using Coop.Mod.Persistence;
using Sync;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod
{
    internal class GameEnvironmentClient : IEnvironmentClient
    {
        public FieldAccessGroup<MobileParty, MovementData> TargetPosition =>
            CampaignMapMovement.Movement;

        public FieldAccess<Campaign, CampaignTimeControlMode> TimeControlMode =>
            TimeControl.TimeControlMode;

        #region Game state access
        public MobileParty GetMobilePartyByIndex(int iPartyIndex)
        {
            return MobileParty.All.SingleOrDefault(p => p.Party.Index == iPartyIndex);
        }

        public Campaign GetCurrentCampaign()
        {
            return Campaign.Current;
        }
        #endregion
    }
}
