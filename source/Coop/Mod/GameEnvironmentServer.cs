using System.Linq;
using Coop.Mod.Patch;
using Coop.Mod.Persistence;
using NLog;
using Sync;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod
{
    public class GameEnvironmentServer : IEnvironmentServer
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public FieldAccessGroup<MobileParty, MovementData> TargetPosition =>
            CampaignMapMovement.Movement;

        public bool CanChangeTimeControlMode => CoopServer.Instance.Current.AreAllClientsPlaying;

        public MobileParty GetMobilePartyByIndex(int iPartyIndex)
        {
            MobileParty ret = null;
            GameLoopRunner.RunOnMainThread(
                () =>
                {
                    ret = MobileParty.All.SingleOrDefault(p => p.Party.Index == iPartyIndex);
                });
            return ret;
        }

        public Campaign GetCurrentCampaign()
        {
            return Campaign.Current;
        }

        public void LockTimeControlStopped()
        {
            Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
            Campaign.Current.SetTimeControlModeLock(true);
        }
        public void UnlockTimeControl()
        {
            Campaign.Current.SetTimeControlModeLock(false);
        }
    }
}
