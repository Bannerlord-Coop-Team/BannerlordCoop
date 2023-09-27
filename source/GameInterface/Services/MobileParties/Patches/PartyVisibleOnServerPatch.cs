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
    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.IsVisible), MethodType.Getter)]
    internal class PartyVisibleOnServerPatch
    {
        private static void Postfix(ref bool __result)
        {
            if(ModInformation.IsServer)
            {
                __result = true;
            }
        }
    }
}
