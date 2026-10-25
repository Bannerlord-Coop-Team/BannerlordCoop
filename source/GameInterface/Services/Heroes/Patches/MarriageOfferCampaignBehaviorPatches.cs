using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(MarriageOfferCampaignBehavior))]
internal class MarriageOfferCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(MarriageOfferCampaignBehavior.DailyTickClan))]
    [HarmonyPrefix]
    private static bool DailyTickClanPrefix(Clan consideringClan)
    {
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<IMarriageOfferCampaignBehaviorInterface>(out var marriageOfferInterface)) return false;

        marriageOfferInterface.DailyTickClan(consideringClan);

        return false;
    }

    [HarmonyPatch(nameof(MarriageOfferCampaignBehavior.HourlyTick))]
    [HarmonyPrefix]
    private static bool HourlyTickPrefix()
    {
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<IMarriageOfferCampaignBehaviorInterface>(out var marriageOfferInterface)) return false;

        marriageOfferInterface.HourlyTick();

        return false;
    }

    [HarmonyPatch(nameof(MarriageOfferCampaignBehavior.OnHeroPrisonerTaken))]
    [HarmonyPrefix]
    private static bool OnHeroPrisonerTakenPrefix(Hero prisoner)
    {
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<IMarriageOfferCampaignBehaviorInterface>(out var marriageOfferInterface)) return false;

        marriageOfferInterface.OnHeroPrisonerTaken(prisoner);

        return false;
    }

    [HarmonyPatch(nameof(MarriageOfferCampaignBehavior.OnHeroesMarried))]
    [HarmonyPrefix]
    private static bool OnHeroesMarriedPrefix(Hero hero1, Hero hero2)
    {
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<IMarriageOfferCampaignBehaviorInterface>(out var marriageOfferInterface)) return false;

        marriageOfferInterface.OnHeroesMarried(hero1, hero2);

        return false;
    }

    [HarmonyPatch(nameof(MarriageOfferCampaignBehavior.OnHeroKilled))]
    [HarmonyPrefix]
    private static bool OnHeroKilledPrefix(Hero victim)
    {
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<IMarriageOfferCampaignBehaviorInterface>(out var marriageOfferInterface)) return false;

        marriageOfferInterface.OnHeroKilled(victim);

        return false;
    }

    [HarmonyPatch(nameof(MarriageOfferCampaignBehavior.OnArmyCreated))]
    [HarmonyPrefix]
    private static bool OnArmyCreatedPrefix()
    {
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<IMarriageOfferCampaignBehaviorInterface>(out var marriageOfferInterface)) return false;

        marriageOfferInterface.OnArmyCreated();

        return false;
    }

    [HarmonyPatch(nameof(MarriageOfferCampaignBehavior.OnMapEventStarted))]
    [HarmonyPrefix]
    private static bool OnMapEventStartedPrefix()
    {
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<IMarriageOfferCampaignBehaviorInterface>(out var marriageOfferInterface)) return false;

        marriageOfferInterface.OnMapEventStarted();

        return false;
    }

    [HarmonyPatch(nameof(MarriageOfferCampaignBehavior.CharacterBecameFugitive))]
    [HarmonyPrefix]
    private static bool CharacterBecameFugitivePrefix()
    {
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<IMarriageOfferCampaignBehaviorInterface>(out var marriageOfferInterface)) return false;

        marriageOfferInterface.CharacterBecameFugitive();

        return false;
    }

    [HarmonyPatch(nameof(MarriageOfferCampaignBehavior.OnWarDeclared))]
    [HarmonyPrefix]
    private static bool OnWarDeclaredPrefix()
    {
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<IMarriageOfferCampaignBehaviorInterface>(out var marriageOfferInterface)) return false;

        marriageOfferInterface.OnWarDeclared();

        return false;
    }

    [HarmonyPatch(nameof(MarriageOfferCampaignBehavior.OnHeroRelationChanged))]
    [HarmonyPrefix]
    private static bool OnHeroRelationChangedPrefix(Hero effectiveHero, Hero effectiveHeroGainedRelationWith)
    {
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<IMarriageOfferCampaignBehaviorInterface>(out var marriageOfferInterface)) return false;

        marriageOfferInterface.OnHeroRelationChanged(effectiveHero, effectiveHeroGainedRelationWith);

        return false;
    }

    [HarmonyPatch(nameof(MarriageOfferCampaignBehavior.OnClanChangedKingdom))]
    [HarmonyPrefix]
    private static bool OnClanChangedKingdomPrefix(Clan clan)
    {
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<IMarriageOfferCampaignBehaviorInterface>(out var marriageOfferInterface)) return false;

        marriageOfferInterface.OnClanChangedKingdom(clan);

        return false;
    }

    [HarmonyPatch(nameof(MarriageOfferCampaignBehavior.OnMarriageOfferAcceptedOnPopUp))]
    [HarmonyPrefix]
    private static bool OnMarriageOfferAcceptedOnPopUpPrefix(MarriageOfferCampaignBehavior __instance)
    {
        // A server cancel can clear the offer while the popup is still open
        if (!__instance.IsThereActiveMarriageOffer) return false;

        MessageBroker.Instance.Publish(__instance, new MarriageOfferAccepted(Hero.MainHero, __instance._currentOfferedPlayerClanHero, __instance._currentOfferedOtherClanHero));

        // The server resolves the marriage, so only clear the local offer like vanilla does after accepting
        __instance.FinalizeMarriageOffer();

        return false;
    }

    [HarmonyPatch(nameof(MarriageOfferCampaignBehavior.OnMarriageOfferDeclinedOnPopUp))]
    [HarmonyPrefix]
    private static bool OnMarriageOfferDeclinedOnPopUpPrefix(MarriageOfferCampaignBehavior __instance)
    {
        if (!__instance.IsThereActiveMarriageOffer) return false;

        // The server answers with a cancel, which clears the local offer like the vanilla decline does
        MessageBroker.Instance.Publish(__instance, new MarriageOfferDeclined(Hero.MainHero, __instance._currentOfferedPlayerClanHero, __instance._currentOfferedOtherClanHero));

        return false;
    }
}
