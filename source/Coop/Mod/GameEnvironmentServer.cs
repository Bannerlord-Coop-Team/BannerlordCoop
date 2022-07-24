using System;
using System.Collections.Generic;
using System.Linq;
using Common;
using Coop.Mod.GameSync;
using Coop.Mod.GameSync.Party;
using Coop.Mod.Patch;
using Coop.Mod.Persistence;
using Coop.Mod.Persistence.Party;
using Coop.Mod.Persistence.RemoteAction;
using NLog;
using RemoteAction;
using Sync;
using Sync.Store;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod
{
    public class GameEnvironmentServer : IEnvironmentServer
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public EventBroadcastingQueue EventQueue => CoopServer.Instance.Persistence?.EventQueue;

        public MobileParty GetMobilePartyById(Guid guid)
        {
            return CoopObjectManager.GetObject<MobileParty>(guid);
        }

        public MobilePartyMovementSync PartySync { get; } = MobilePartyManaged.MovementSync;

        public RemoteStoreServer Store =>
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

        public void SetMovement(MobileParty party, MovementData data)
        {
            MobilePartyManaged.AuthoritativeMovementChange(party, data);
        }
    }
}
