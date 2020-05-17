using System.Linq;
using Coop.Game.Patch;
using Coop.Game.Persistence;
using Coop.Game.Persistence.Party;
using Coop.Sync;
using TaleWorlds.CampaignSystem;

namespace Coop.Game
{
    public class GameEnvironmentServer : IEnvironmentServer
    {
        public SyncFieldGroup<MobileParty, MovementData> TargetPosition =>
            CampaignMapMovement.Movement;

        public MobileParty GetMobilePartyByIndex(int iPartyIndex)
        {
            return MobileParty.All.SingleOrDefault(p => p.Party.Index == iPartyIndex);
        }

        public Campaign GetCurrentCampaign()
        {
            return Campaign.Current;
        }
    }
}
