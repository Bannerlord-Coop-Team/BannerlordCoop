using System.Linq;
using Coop.Mod.Patch;
using Coop.Mod.Persistence;
using Coop.Mod.Persistence.Party;
using Coop.Sync;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod
{
    internal class GameEnvironmentClient : IEnvironmentClient
    {
        public SyncFieldGroup<MobileParty, MovementData> TargetPosition =>
            CampaignMapMovement.Movement;

        public SyncField<Campaign, CampaignTimeControlMode> TimeControlMode =>
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
