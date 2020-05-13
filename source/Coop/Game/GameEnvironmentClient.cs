using System.Linq;
using Coop.Game.Patch;
using Coop.Game.Persistence;
using Coop.Sync;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Game
{
    internal class GameEnvironmentClient : IEnvironmentClient
    {
        public SyncField<MobileParty, Vec2> TargetPosition => CampaignMapMovement.TargetPosition;

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
