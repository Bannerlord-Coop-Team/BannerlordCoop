using Common.Messaging;
using GameInterface;
using GameInterface.Services.Clans;
using HarmonyLib;
using NavalDLC.GauntletUI.Clan;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;

namespace Coop.Naval.Clans.Patches;

[HarmonyPatch(typeof(NavalGauntletClanScreen))]
internal class NavalGauntletClanScreenPatches
{
    // The naval screen overrides GauntletClanScreen.CreateDataSource, so the coop prefix on the base never runs
    [HarmonyPatch(nameof(NavalGauntletClanScreen.CreateDataSource))]
    [HarmonyPrefix]
    public static bool CreateDataSourcePrefix(NavalGauntletClanScreen __instance, ref ClanManagementVM __result)
    {
        ContainerProvider.TryResolve<IClanFinance>(out var finance);
        ContainerProvider.TryResolve<IMessageBroker>(out var messages);

        __result = new CoopNavalClanManagementVM(__instance.CloseClanScreen, __instance.ShowHeroOnMap,
            __instance.OpenPartyScreenForNewClanParty, __instance.OpenBannerEditorWithPlayerClan, finance, messages);
        return false;
    }
}
