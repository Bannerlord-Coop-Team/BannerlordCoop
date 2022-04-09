using System;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.GameSync.Party
{
    /// <summary>
    ///     Listens to campaign events that influence the lifetime of <see cref="MobileParty"/> instances.
    /// </summary>
    public class MobilePartyLifetimeBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnPartyDisbandedEvent.AddNonSerializedListener(this, OnPartyDisbanded);
            CampaignEvents.OnPartyRemovedEvent.AddNonSerializedListener(this, OnPartyRemoved);
            CampaignEvents.MobilePartyCreated.AddNonSerializedListener(this, OnPartyAdded);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        /// <summary>
        ///     Listener for <see cref="CampaignEvents.MobilePartyCreated"/>.
        /// </summary>
        /// <param name="party"></param>
        private void OnPartyAdded(MobileParty party)
        {
            MobilePartyManaged.MakeManaged(party);
        }

        /// <summary>
        ///     Listener for <see cref="CampaignEvents.OnPartyRemovedEvent"/>.
        /// </summary>
        /// <param name="party"></param>
        private void OnPartyRemoved(PartyBase party)
        {
            if (Coop.IsServer)
            {
                CoopServer.Instance.Persistence.MobilePartyEntityManager.RemoveParty(party.MobileParty);
            }
        }

        /// <summary>
        ///     Listener for <see cref="CampaignEvents.OnPartyDisbandedEvent"/>.
        /// </summary>
        /// <param name="party"></param>
        /// <param name="settlement"></param>
        private void OnPartyDisbanded(MobileParty party, Settlement settlement)
        {
            if (Coop.IsServer)
            {
                CoopServer.Instance.Persistence.MobilePartyEntityManager.RemoveParty(party);
            }
        }
    }
}
