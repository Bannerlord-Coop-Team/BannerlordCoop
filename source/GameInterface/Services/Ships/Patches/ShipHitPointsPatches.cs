using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Ships.Patches;

/// <summary>[Client] Refreshes the open campaign menu when the main party's ship hit points change.</summary>
[HarmonyPatch(typeof(Ship))]
internal class ShipHitPointsPatches
{
    // Port menu options like repair ships read the main party's ship hit points. Skipped during a mission, where the menu
    // context can have no handler; MapState.OnActivate refreshes the menu on return.
    [HarmonyPatch(nameof(Ship.HitPoints), MethodType.Setter)]
    [HarmonyPostfix]
    public static void HitPointsSetterPostfix(Ship __instance)
    {
        if (ModInformation.IsServer) return;
        if (__instance.Owner == null || __instance.Owner != PartyBase.MainParty) return;
        if (Mission.Current != null) return;

        var menuContext = Campaign.Current?.CurrentMenuContext;
        if (menuContext == null) return;

        Campaign.Current.GameMenuManager.RefreshMenuOptions(menuContext);
    }
}
