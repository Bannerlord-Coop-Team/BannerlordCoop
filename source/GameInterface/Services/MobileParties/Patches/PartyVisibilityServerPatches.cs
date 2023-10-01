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
    internal class PartyIsVisibleOnServerPatch
    {
        private static void Postfix(ref bool __result)
        {
            if(ModInformation.IsServer)
            {
                __result = true;
            }
        }
    }

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
