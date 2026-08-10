using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>Keeps registered remote player escorts eligible for movement ticks after reaching their target.</summary>
[HarmonyPatch(typeof(MobileParty), nameof(MobileParty.IsMoving), MethodType.Getter)]
internal class PlayerEscortIsMovingPatch
{
    [HarmonyPostfix]
    static void Postfix(MobileParty __instance, ref bool __result)
    {
        if (__result ||
            __instance.IsMainParty ||
            !__instance.IsPlayerParty() ||
            __instance.DefaultBehavior != AiBehavior.EscortParty ||
            __instance.TargetParty?.IsActive != true)
            return;

        __result = __instance.IsActive &&
            !__instance.IsTransitionInProgress &&
            __instance.CurrentSettlement == null &&
            __instance.MapEvent == null &&
            __instance.BesiegedSettlement == null;
    }
}
