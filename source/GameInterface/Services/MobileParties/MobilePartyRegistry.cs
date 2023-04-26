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
using System.Collections;
using GameInterface.Services.Registry;

namespace GameInterface.Services.MobileParties
{
    internal interface IMobilePartyRegistry : IRegistry<MobileParty>
    {
        void RegisterAllParties();
    }

    internal class MobilePartyRegistry : RegistryBase<MobileParty>, IMobilePartyRegistry
    {
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
                if(RegisterExistingObject(party.StringId, party) == false)
                {
                    Logger.Warning("Unable to register party: {object}", party.Name);
                }
            }
        }
    }
}
