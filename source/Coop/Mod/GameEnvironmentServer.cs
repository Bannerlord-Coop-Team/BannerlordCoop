using System;
using System.Collections.Generic;
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

        private Dictionary<MBGUID, MobileParty> m_PartyCache = new Dictionary<MBGUID, MobileParty>();

        public MobileParty GetMobilePartyById(MBGUID guid)
        {
            if (!m_PartyCache.TryGetValue(guid, out MobileParty ret))
            {
                GameLoopRunner.RunOnMainThread(
                    () =>
                    {
                        // Update the whole cache since we're already in the game loop thread. Doesn't happen that often.
                        m_PartyCache = MobileParty.All.AsParallel().ToDictionary(party => party.Id);
                        ret = m_PartyCache[guid];
                    });
            }
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
