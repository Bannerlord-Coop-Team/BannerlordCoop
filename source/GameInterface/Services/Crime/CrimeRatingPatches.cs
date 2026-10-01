using Common;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Players;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Crime;

[HarmonyPatch]
internal class CrimeRatingGetterPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(Kingdom), nameof(Kingdom.MainHeroCrimeRating));
        yield return AccessTools.PropertyGetter(typeof(Clan), nameof(Clan.MainHeroCrimeRating));
    }

    private static bool Prefix(IFaction __instance, ref float __result)
    {
        if (!ContainerProvider.TryResolve<ICrimeRatingService>(out var ratings)) return true;
        __result = ratings.Get(__instance);
        return false;
    }
}

[HarmonyPatch]
internal class CrimeRatingSetterPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertySetter(typeof(Kingdom), nameof(Kingdom.MainHeroCrimeRating));
        yield return AccessTools.PropertySetter(typeof(Clan), nameof(Clan.MainHeroCrimeRating));
    }

    private static bool Prefix(IFaction __instance, float value)
    {
        if (!ContainerProvider.TryResolve<ICrimeRatingService>(out var ratings)) return true;
        ratings.Set(__instance, value);
        return false;
    }
}

[HarmonyPatch(typeof(ChangeCrimeRatingAction), nameof(ChangeCrimeRatingAction.ApplyInternal))]
internal class CrimeRatingActionPatch
{
    private static bool Prefix(IFaction faction, float deltaCrimeRating, bool showNotification)
    {
        if (ModInformation.IsServer) return ResolvedMainHeroContext.ResolvedMainHero != null;
        if (ContainerProvider.TryResolve<ICrimeRatingService>(out var ratings))
            ratings.Request(faction, deltaCrimeRating, showNotification);
        return false;
    }
}

[HarmonyPatch(typeof(BeHostileAction), nameof(BeHostileAction.ApplyGeneralConsequencesOnPeace))]
internal class HostileCrimeContextPatch
{
    private static bool Prefix(PartyBase attackerParty, out MainHeroSubstitutionScope __state)
    {
        __state = null;
        if (ModInformation.IsClient) return false;
        if (attackerParty.LeaderHero != null && PlayerManager.TryGetControlledObjectInfo(attackerParty.LeaderHero, out _))
            __state = new MainHeroSubstitutionScope(attackerParty.LeaderHero, attackerParty.MobileParty);
        return true;
    }

    private static void Finalizer(MainHeroSubstitutionScope __state) => __state?.Dispose();
}
