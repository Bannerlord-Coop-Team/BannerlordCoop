using Common;
using GameInterface.Services.Crime;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Clans.Patches.Disable;

[HarmonyPatch(typeof(CrimeCampaignBehavior))]
internal class DisableCrimeCampaignBehavior
{
    [HarmonyPatch(nameof(CrimeCampaignBehavior.OnAfterGameCreated))]
    [HarmonyPrefix]
    private static bool Menus() => ModInformation.IsClient;

    [HarmonyPatch(nameof(CrimeCampaignBehavior.OnDailyTick))]
    [HarmonyPrefix]
    private static bool DailyTick()
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<ICrimeRatingService>(out var ratings))
            ratings.DailyTick();
        return false;
    }

    [HarmonyPatch(nameof(CrimeCampaignBehavior.OnMakePeace))]
    [HarmonyPrefix]
    private static bool MakePeace(IFaction side1Faction, IFaction side2Faction)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<ICrimeRatingService>(out var ratings))
            ratings.MakePeace(side1Faction, side2Faction);
        return false;
    }

    [HarmonyPatch(nameof(CrimeCampaignBehavior.OnHeroDeath))]
    [HarmonyPrefix]
    private static bool HeroDeath(Hero victim)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<ICrimeRatingService>(out var ratings))
            ratings.HeroDied(victim);
        return false;
    }
}
