using System;
using System.Linq;
using Coop.Mod.Patch;
using Coop.Mod.Patch.MobilePartyPatches;
using Coop.Mod.Persistence;
using Coop.Mod.Persistence.Party;
using Coop.Mod.Persistence.RemoteAction;
using NLog;
using RemoteAction;
using Sync;
using Sync.Store;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod
{
    public class GameEnvironmentServer : IEnvironmentServer
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public EventBroadcastingQueue EventQueue => CoopServer.Instance.Persistence?.EventQueue;

        public MobileParty GetMobilePartyById(MBGUID guid)
        {
            MobileParty ret = null;
            GameLoopRunner.RunOnMainThread(
                () =>
                {
                    ret = MobileParty.All.SingleOrDefault(p => p.Id == guid);
                });
            return ret;
        }

        public MobilePartySync PartySync { get; } = CampaignMapMovement.Sync;

        public SharedRemoteStore Store =>
            CoopServer.Instance.SyncedObjectStore ??
            throw new InvalidOperationException("Client not initialized.");

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
