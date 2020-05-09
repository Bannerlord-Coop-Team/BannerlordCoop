using System.Linq;
using Coop.Game.Persistence;
using TaleWorlds.CampaignSystem;

namespace Coop.Game
{
    public class GameEnvironmentServer : IEnvironmentServer
    {
        public CampaignTimeControlMode TimeControlMode { get; set; } = CampaignTimeControlMode.Stop;

        public MobileParty GetMobilePartyByIndex(int iPartyIndex)
        {
            return MobileParty.All.SingleOrDefault(p => p.Party.Index == iPartyIndex);
        }
    }
}
