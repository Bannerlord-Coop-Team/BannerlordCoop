using GameInterface.Services.ItemRosters;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyBases.Patches;

[HarmonyPatch(typeof(PartyBase))]
internal class PartyBasePatch
{
    [HarmonyPatch(nameof(PartyBase.ItemRoster), MethodType.Setter)]
    [HarmonyPostfix]
    public static void ItemRosterSetterPostfix(ref PartyBase __instance)
    {
        if (ModInformation.IsClient) return;

        ItemRosterLookup.Set(__instance.ItemRoster, __instance);
    }
}
