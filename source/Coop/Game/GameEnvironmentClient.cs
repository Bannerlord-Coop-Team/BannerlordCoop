using System.Linq;
using Coop.Game.Patch;
using Coop.Game.Persistence;
using Coop.Game.Persistence.Party;
using Coop.Sync;
using TaleWorlds.CampaignSystem;

namespace Coop.Game
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
