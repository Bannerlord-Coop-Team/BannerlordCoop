using System.Linq;
using Coop.Game.Patch;
using Coop.Game.Persistence;
using Coop.Sync;
using TaleWorlds.CampaignSystem;

namespace Coop.Game
{
    internal class GameEnvironmentClient : IEnvironmentClient
    {
        public SyncField TargetPosition => CampaignMapMovement.TargetPosition;
        public SyncField TimeControlMode => TimeControl.TimeControlMode;

        #region Game state access
        public MobileParty GetMobilePartyByIndex(int iPartyIndex)
        {
            return MobileParty.All.SingleOrDefault(p => p.Party.Index == iPartyIndex);
        }

        public object GetTimeController()
        {
            return Campaign.Current;
        }
        #endregion
    }
}
