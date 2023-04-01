using Common;
using Common.Messaging;
using GameInterface.Services.MobileParties.Messages;
using Microsoft.Win32;
using System.Collections.Generic;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Common.Logging;
using GameInterface.Services.Heroes;
using Serilog;

namespace GameInterface.Services.MobileParties
{
    internal interface IMobilePartyRegistry : IRegistryBase<MobileParty>
    {
        void RegisterAllParties();
        void RegisterPartiesWithStringIds(IReadOnlyDictionary<string, Guid> stringIdToGuids);
    }

    internal class MobilePartyRegistry : RegistryBase<MobileParty>, IMobilePartyRegistry
    {
        private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyRegistry>();

        public void RegisterAllParties()
        {
            var objectManager = Campaign.Current?.CampaignObjectManager;

            if (objectManager == null)
            {
                Logger.Error("Unable to register objects when CampaignObjectManager is null");
                return;
            }

            foreach (var party in objectManager.MobileParties)
            {
                RegisterNewObject(party);
            }
        }

        public void RegisterPartiesWithStringIds(IReadOnlyDictionary<string, Guid> stringIdToGuids)
        {
            var objectManager = Campaign.Current?.CampaignObjectManager;

            if (objectManager == null)
            {
                Logger.Error("CampaignObjectManager was null when trying to register parties");
                return;
            }

            // Error recording lists
            var unregisteredParties = new List<string>();
            var badGuidParties = new List<string>();

            foreach (var party in objectManager.MobileParties)
            {
                if (stringIdToGuids.TryGetValue(party.StringId, out Guid id))
                {
                    if (id != Guid.Empty)
                    {
                        RegisterExistingObject(id, party);
                    }
                    else
                    {
                        // Parties with empty guids
                        badGuidParties.Add(party.StringId);
                    }
                }
                else
                {
                    // Existing parties that don't exist in stringIds
                    unregisteredParties.Add(party.StringId);
                }
            }

            // Log any bad guids if they exist
            if (badGuidParties.IsEmpty() == false)
            {
                Logger.Error("The following parties had incorrect Guids: {parties}", badGuidParties);
            }

            // Log any unregistered parties if they exist
            if (unregisteredParties.IsEmpty() == false)
            {
                Logger.Error("The following parties were not registered: {parties}", unregisteredParties);
            }
        }
    }
}
