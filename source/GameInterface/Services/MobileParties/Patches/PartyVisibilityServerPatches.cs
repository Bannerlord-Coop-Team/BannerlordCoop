using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Control;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.MobileParties.Patches
{
    /// <summary>
    /// 
    /// Parties are always visible on server
    /// 
    /// </summary>
    [HarmonyPatch(typeof(PartyBase), nameof(PartyBase.OnVisibilityChanged))]
    internal class PartyVisibilityOnServerPatch
    {
        private static void Prefix(ref bool value)
        {
            if (ModInformation.IsServer)
            {
                value = true;
            }
        }
    }
}
