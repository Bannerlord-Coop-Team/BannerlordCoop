using Common;
using HarmonyLib;
using SandBox.ViewModelCollection.Nameplate;

namespace GameInterface.Services.UI.Patches;

/// <summary>
/// Rebuilds a party nameplate's banner icon once the party's banner data exists.
/// On clients a newly spawned party can be spotted before its clan and party
/// component have synced, so the nameplate caches an empty banner image and
/// nothing marks it dirty again afterwards.
/// </summary>
[HarmonyPatch(typeof(PartyNameplateVM))]
internal class PartyNameplateBannerPatch
{
    [HarmonyPatch(nameof(PartyNameplateVM.RefreshDynamicProperties))]
    [HarmonyPostfix]
    private static void Postfix(PartyNameplateVM __instance)
    {
        if (ModInformation.IsServer) return;

        if (__instance.PartyBanner == null || !__instance.PartyBanner.IsEmpty) return;

        // Only re-dirty once the banner would produce a non-empty image,
        // otherwise an empty banner code would trigger a rebuild every frame.
        var banner = __instance.Party?.Banner;
        if (banner == null || string.IsNullOrEmpty(banner.BannerCode)) return;

        __instance._isPartyBannerDirty = true;
    }
}
