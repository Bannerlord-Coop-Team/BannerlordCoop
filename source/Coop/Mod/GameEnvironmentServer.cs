using System.Linq;
using Coop.Mod.Patch;
using Coop.Mod.Persistence;
using Sync;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod
{
    public class GameEnvironmentServer : IEnvironmentServer
    {
        public FieldAccessGroup<MobileParty, MovementData> TargetPosition =>
            CampaignMapMovement.Movement;

        public bool CanChangeTimeControlMode => CoopServer.Instance.Current.AreAllClientsPlaying;

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
