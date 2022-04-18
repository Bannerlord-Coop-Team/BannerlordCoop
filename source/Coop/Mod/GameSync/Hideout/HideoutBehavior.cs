using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.GameSync.Hideout
{
    class HideoutBehavior : CampaignBehaviorBase
    {
        /// <summary>
        ///     Store that lets you know which player has already seen a hideout.
        /// </summary>
        private readonly Dictionary<MBGUID, List<MBGUID>> settlementsSpotted = new Dictionary<MBGUID, List<MBGUID>>();

        /// <summary>
        ///     Register events related to Hideouts.
        /// </summary>
        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickSettlementEvent.AddNonSerializedListener(this, OnHourlyTickSettlement);
            CampaignEvents.OnHideoutDeactivatedEvent.AddNonSerializedListener(this, OnHideoutDeactivated);
        }

        /// <summary>
        ///     Listener for <see cref="CampaignEvents.HourlyTickSettlementEvent"/>.
        ///     Used for detecting an hideout around players.
        /// </summary>
        /// <param name="settlement">Settlement</param>
        public void OnHourlyTickSettlement(Settlement settlement)
        {
            if (!Coop.IsServer)
            {
                return;
            }

            bool canBeSpotted = settlement.IsHideout && settlement.Hideout.IsInfested;
            int spottedByCount = settlementsSpotted.TryGetValue(settlement.Id, out var spottedBy) ? spottedBy.Count : 0;
            int maxSpottedByCount = CoopServer.Instance.Persistence.MobilePartyEntityManager.PlayerControlledParties.Count;

            if (canBeSpotted && spottedByCount < maxSpottedByCount)
            {
                if (!settlementsSpotted.ContainsKey(settlement.Id))
                {
                    settlementsSpotted.Add(settlement.Id, new List<MBGUID>());
                }

                foreach (var playerControlledParty in CoopServer.Instance.Persistence.MobilePartyEntityManager.PlayerControlledParties)
                {
                   List<MBGUID> playersSettlement = settlementsSpotted[settlement.Id];
                    if (playersSettlement.Contains(playerControlledParty.Id))
                    {
                        return;
                    }

                    float hideoutSpottingDistance = (playerControlledParty.HasPerk(DefaultPerks.Scouting.RumourNetwork, true))
                        ? playerControlledParty.SeeingRange * 1.2f * (1f + DefaultPerks.Scouting.RumourNetwork.SecondaryBonus * 0.01f)
                        : playerControlledParty.SeeingRange * 1.2f;

                    float partyDistanceSquared = playerControlledParty.Position2D.DistanceSquared(settlement.Position2D);
                    bool isSpotted = partyDistanceSquared < hideoutSpottingDistance * hideoutSpottingDistance;

                    if (isSpotted && settlement.Parties.Count > 0 && MBRandom.RandomFloat < partyDistanceSquared)
                    {
                        playersSettlement.Add(playerControlledParty.Id);
                        HideoutSync.BroadcastHideoutDiscovery(playerControlledParty, settlement);
                    }
                }
            }
        }

        /// <summary>
        ///     Listener for <see cref="CampaignEvents.OnHideoutDeactivatedEvent"/>.
        ///     Used for detecting when an hideout is empty.
        /// </summary>
        /// <param name="settlement"></param>
        public void OnHideoutDeactivated(Settlement settlement)
        {
            if (!Coop.IsServer)
            {
                return;
            }

            settlementsSpotted.Remove(settlement.Id);
            HideoutSync.BroadcastHideoutRemoval(settlement);
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
