using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// The server and debug visibility overrides reveal active parties. Release clients
/// use native fog of war for active parties. Parked and destroyed parties read as
/// unspotted everywhere, including when nameplates read <see cref="MobileParty.IsVisible"/>.
/// </summary>
[HarmonyPatch(typeof(MobileParty), nameof(MobileParty.IsVisible), MethodType.Getter)]
internal class PartyIsVisibleGetterServerPatch
{
    internal static void Postfix(MobileParty __instance, ref bool __result)
    {
        __result = __instance.IsActive && (ModInformation.IsServer || DebugPartyVisibility.ForceAllVisible || __result);
    }
}

[HarmonyPatch(typeof(MobileParty))]
internal class PartyVisibilityOnServerPatch
{
    [HarmonyPatch(nameof(MobileParty.IsVisible), MethodType.Setter)]
    [HarmonyPrefix]
    internal static void PrefixIsVisible(MobileParty __instance, ref bool value)
    {
#if DEBUG
        // Restore the exact captured fixture state through the native visibility setter.
        if (global::GameInterface.Services.SiegeEvents.Commands.DefenderSiegeFixtureCommands.IsRestoringCapturedVisibility(__instance))
            return;
#endif
        if (ModInformation.IsServer || DebugPartyVisibility.ForceAllVisible)
        {
            value = __instance.IsActive;
        }
    }

    [HarmonyPatch(nameof(MobileParty.IsInspected), MethodType.Setter)]
    [HarmonyPrefix]
    internal static void PrefixIsInspected(ref bool value)
    {
        if (ModInformation.IsServer || DebugPartyVisibility.ForceAllVisible)
        {
            value = true;
        }
    }
}
