using Common;
using HarmonyLib;
using SandBox.ViewModelCollection.Nameplate;
using System.Runtime.CompilerServices;

namespace GameInterface.Services.UI.Patches;

/// <summary>
/// Rebuilds a party nameplate's banner icon after delayed sync or a faction change.
/// </summary>
[HarmonyPatch(typeof(PartyNameplateVM))]
internal class PartyNameplateBannerPatch
{
    private static readonly ConditionalWeakTable<PartyNameplateVM, PartyBannerCodeTracker> bannerTrackers = new();

    [HarmonyPatch(nameof(PartyNameplateVM.RefreshDynamicProperties))]
    [HarmonyPostfix]
    private static void Postfix(PartyNameplateVM __instance)
    {
        if (ModInformation.IsServer) return;

        var bannerCode = __instance.Party?.Banner?.BannerCode;
        if (string.IsNullOrEmpty(bannerCode)) return;

        // AutoSync changes Clan._kingdom without the native OnClanChangeKingdom callback.
        var bannerChanged = bannerTrackers
            .GetValue(__instance, _ => new PartyBannerCodeTracker())
            .Update(bannerCode);

        if (bannerChanged)
        {
            __instance.Party?.Party?.SetVisualAsDirty();
        }

        if (bannerChanged || __instance.PartyBanner == null || __instance.PartyBanner.IsEmpty)
        {
            __instance._isPartyBannerDirty = true;
        }
    }
}

internal sealed class PartyBannerCodeTracker
{
    private string bannerCode;
    private bool hasBannerCode;

    internal bool Update(string newBannerCode)
    {
        bool changed = hasBannerCode && bannerCode != newBannerCode;
        bannerCode = newBannerCode;
        hasBannerCode = true;
        return changed;
    }
}
