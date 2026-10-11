using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Ships.Patches;

/// <summary>
/// A replicated <see cref="Ship.Owner"/> runs only the setter on clients, without the ship owner actions' naval visual
/// refresh of both parties.
/// </summary>
[HarmonyPatch(typeof(Ship))]
internal class ShipOwnerVisualPatches
{
    [HarmonyPatch(nameof(Ship.Owner), MethodType.Setter)]
    [HarmonyPrefix]
    private static void OwnerSetterPrefix(Ship __instance, out PartyBase __state)
    {
        __state = __instance.Owner;
    }

    [HarmonyPatch(nameof(Ship.Owner), MethodType.Setter)]
    [HarmonyPostfix]
    private static void OwnerSetterPostfix(Ship __instance, PartyBase __state)
    {
        if (ModInformation.IsServer) return;
        if (__state == __instance.Owner) return;

        __state?.MobileParty?.SetNavalVisualAsDirty();
        __instance.Owner?.MobileParty?.SetNavalVisualAsDirty();
    }
}
