using GameInterface.Services.ItemRosters;
using HarmonyLib;
using System;
using System.ComponentModel;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.PartyBases.Patches;

[HarmonyPatch(typeof(PartyBase))]
internal class PartyBasePatch
{
    [HarmonyPatch(nameof(PartyBase.ItemRoster), MethodType.Setter)]
    [HarmonyPostfix]
    public static void ItemRosterSetterPostfix(PartyBase __instance)
    {
        if (ModInformation.IsClient) return;

        ItemRosterLookup.Set(__instance.ItemRoster, __instance);
    }
}
