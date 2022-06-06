using Coop.Mod.GameSync.Party;
using Coop.Mod.GameSync.Roster;
using Coop.Mod.Persistence.Party;
using NLog;
using RailgunNet.Logic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Mod.GameSync
{
    public class Initializer
    {
        /// <summary>
        ///     Called after a game has been fully loaded in order to setup the game sync.
        /// </summary>
        public static void SetupSyncAfterLoad()
        {
            foreach (MobileParty party in MobileParty.All)
            {
                MobilePartyManaged.MakeManaged(party, false);
            }

            if(Coop.IsServer)
            {
                CoopServer.Instance.Persistence.MobilePartyEntityManager.OnBeforePartyScopeEnter += OnBeforePartyScopeEnter;
            }
        }

        /// <summary>
        ///     Called just before a mobile party enters the scope of a client.
        /// </summary>
        /// <param name="controller">The client whose scope the party enters</param>
        /// <param name="entity">The entity of the party entering the scope</param>
        private static void OnBeforePartyScopeEnter(RailController controller, MobilePartyEntityServer entity)
        {
            MobileParty party = entity.Instance;
            if(party == null)
            {
                Logger.Error($"{entity} has no valid MobileParty instance. Invalid state, sync not possible.");
                return;
            }

            Logger.Trace($"{entity} entered scope of {controller}");

            // Roster might be out of date. We could keep track of what we last sent the client and only send an update if
            // it is actually outdated. But as of right now, this does seems like a lot of effort for little benefit. Easier
            // to just always send everything.
            TroopRosterSync.BroadcastTroopRosterChange(party, party.MemberRoster);
            TroopRosterSync.BroadcastTroopRosterChange(party, party.PrisonRoster);
        }

        private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();
    }
}
