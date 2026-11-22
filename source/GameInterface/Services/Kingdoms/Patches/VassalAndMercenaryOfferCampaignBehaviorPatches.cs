using Common;
using Common.Messaging;
using GameInterface.Services.Kingdoms.Interfaces;
using GameInterface.Services.Kingdoms.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Kingdoms.Patches;

[HarmonyPatch(typeof(VassalAndMercenaryOfferCampaignBehavior))]
internal class VassalAndMercenaryOfferCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(VassalAndMercenaryOfferCampaignBehavior.RegisterEvents))]
    [HarmonyPrefix]
    public static void RegisterEventsPrefix(VassalAndMercenaryOfferCampaignBehavior __instance)
    {
        // Stopping offers is tracked per player now, older saves can still have the shared flag set
        __instance._stopOffers = false;
    }

    [HarmonyPatch(nameof(VassalAndMercenaryOfferCampaignBehavior.DailyTick))]
    [HarmonyPrefix]
    public static bool DailyTickPrefix(VassalAndMercenaryOfferCampaignBehavior __instance)
    {
        if (ModInformation.IsClient) return false;

        if (!ContainerProvider.TryResolve<IVassalAndMercenaryOfferCampaignBehaviorInterface>(out var vassalAndMercenaryOfferCampaignBehaviorInterface)) return false;

        vassalAndMercenaryOfferCampaignBehaviorInterface.DailyTick();

        return false;
    }

    [HarmonyPatch(nameof(VassalAndMercenaryOfferCampaignBehavior.OnHeroPrisonerTaken))]
    [HarmonyPrefix]
    public static bool OnHeroPrisonerTakenPrefix(VassalAndMercenaryOfferCampaignBehavior __instance, PartyBase captor, Hero prisoner)
    {
        if (ModInformation.IsClient) return false;

        if (!ContainerProvider.TryResolve<IVassalAndMercenaryOfferCampaignBehaviorInterface>(out var vassalAndMercenaryOfferCampaignBehaviorInterface)) return false;

        vassalAndMercenaryOfferCampaignBehaviorInterface.OnHeroPrisonerTaken(prisoner);

        return false;
    }

    [HarmonyPatch(nameof(VassalAndMercenaryOfferCampaignBehavior.OnClanChangedKingdom))]
    [HarmonyPrefix]
    public static bool OnClanChangedKingdomPrefix(VassalAndMercenaryOfferCampaignBehavior __instance, Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification)
    {
        if (ModInformation.IsClient) return false;

        if (!ContainerProvider.TryResolve<IVassalAndMercenaryOfferCampaignBehaviorInterface>(out var vassalAndMercenaryOfferCampaignBehaviorInterface)) return false;

        vassalAndMercenaryOfferCampaignBehaviorInterface.OnClanChangedKingdom(clan, newKingdom, detail);

        return false;
    }

    [HarmonyPatch(nameof(VassalAndMercenaryOfferCampaignBehavior.OnVassalOrMercenaryServiceOfferedToPlayer))]
    [HarmonyPrefix]
    public static bool OnVassalOrMercenaryServiceOfferedToPlayerPrefix() => ModInformation.IsClient;

    [HarmonyPatch(nameof(VassalAndMercenaryOfferCampaignBehavior.OnWarDeclared))]
    [HarmonyPrefix]
    public static bool OnWarDeclaredPrefix(VassalAndMercenaryOfferCampaignBehavior __instance, IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
    {
        if (ModInformation.IsClient) return false;

        if (!ContainerProvider.TryResolve<IVassalAndMercenaryOfferCampaignBehaviorInterface>(out var vassalAndMercenaryOfferCampaignBehaviorInterface)) return false;

        vassalAndMercenaryOfferCampaignBehaviorInterface.OnWarDeclared(faction1, faction2);

        return false;
    }

    [HarmonyPatch(nameof(VassalAndMercenaryOfferCampaignBehavior.OnHeroRelationChanged))]
    [HarmonyPrefix]
    public static bool OnHeroRelationChangedPrefix(VassalAndMercenaryOfferCampaignBehavior __instance, Hero effectiveHero, Hero effectiveHeroGainedRelationWith)
    {
        if (ModInformation.IsClient) return false;

        if (!ContainerProvider.TryResolve<IVassalAndMercenaryOfferCampaignBehaviorInterface>(out var vassalAndMercenaryOfferCampaignBehaviorInterface)) return false;

        vassalAndMercenaryOfferCampaignBehaviorInterface.OnHeroRelationChanged(effectiveHero, effectiveHeroGainedRelationWith);

        return false;
    }

    [HarmonyPatch(nameof(VassalAndMercenaryOfferCampaignBehavior.OnKingdomDestroyed))]
    [HarmonyPrefix]
    public static bool OnKingdomDestroyedPrefix(VassalAndMercenaryOfferCampaignBehavior __instance, Kingdom destroyedKingdom)
    {
        if (ModInformation.IsClient) return false;

        if (!ContainerProvider.TryResolve<IVassalAndMercenaryOfferCampaignBehaviorInterface>(out var vassalAndMercenaryOfferCampaignBehaviorInterface)) return false;

        vassalAndMercenaryOfferCampaignBehaviorInterface.OnKingdomDestroyed(destroyedKingdom);

        return false;
    }

    // Offers belong to the clan, which an heir keeps, so they carry over with the migrated session data
    [HarmonyPatch(nameof(VassalAndMercenaryOfferCampaignBehavior.OnPlayerCharacterChanged))]
    [HarmonyPrefix]
    public static bool OnPlayerCharacterChangedPrefix() => false;

    [HarmonyPatch(nameof(VassalAndMercenaryOfferCampaignBehavior.CancelVassalOrMercenaryServiceOffer))]
    [HarmonyPrefix]
    public static void CancelVassalOrMercenaryServiceOfferPrefix(VassalAndMercenaryOfferCampaignBehavior __instance, Kingdom kingdom)
    {
        // Server cancels clear the offer before reaching here, so only the player's own cancels get sent
        if (ModInformation.IsClient && (__instance._currentMercenaryOffer?.Item1 == kingdom || __instance._vassalOffers.ContainsKey(kingdom)))
        {
            MessageBroker.Instance.Publish(__instance, new VassalOrMercenaryOfferDeclined(Hero.MainHero, kingdom));
        }
    }

    [HarmonyPatch(nameof(VassalAndMercenaryOfferCampaignBehavior.MercenaryOfferAccepted))]
    [HarmonyPrefix]
    public static bool MercenaryOfferAcceptedPrefix(VassalAndMercenaryOfferCampaignBehavior __instance)
    {
        // The server may have cancelled the offer while the popup was open
        Kingdom kingdom = __instance._currentMercenaryOffer?.Item1;
        if (kingdom == null) return false;

        __instance.ClearKingdomOffer(kingdom);
        MessageBroker.Instance.Publish(__instance, new MercenaryOfferAccepted(Hero.MainHero, kingdom));

        return false;
    }

    [HarmonyPatch(nameof(VassalAndMercenaryOfferCampaignBehavior.MercenaryOfferDeclined))]
    [HarmonyPrefix]
    public static bool MercenaryOfferDeclinedPrefix(VassalAndMercenaryOfferCampaignBehavior __instance)
    {
        Kingdom kingdom = __instance._currentMercenaryOffer?.Item1;
        if (kingdom == null) return false;

        __instance.ClearKingdomOffer(kingdom);
        MessageBroker.Instance.Publish(__instance, new VassalOrMercenaryOfferDeclined(Hero.MainHero, kingdom));

        return false;
    }
}
