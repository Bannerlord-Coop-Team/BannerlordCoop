using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Kingdoms.Messages.Collections;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Kingdoms.Patches;

/// <summary>
/// Publishes server-authoritative updates for Kingdom backing collections that vanilla mutates through internal helpers.
/// </summary>
[HarmonyPatch(typeof(Kingdom))]
internal class KingdomCollectionPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<KingdomCollectionPatches>();

    [HarmonyPatch(nameof(Kingdom.AddArmyInternal))]
    [HarmonyPrefix]
    private static bool AddArmyPrefix(Kingdom __instance, Army army)
    {
        return PublishSingleChange(
            nameof(Kingdom.AddArmyInternal),
            __instance,
            army,
            new ArmyListUpdated(__instance, army),
            __instance._armies?.Contains(army) != true);
    }

    [HarmonyPatch(nameof(Kingdom.RemoveArmyInternal))]
    [HarmonyPrefix]
    private static bool RemoveArmyPrefix(Kingdom __instance, Army army)
    {
        return PublishSingleChange(
            nameof(Kingdom.RemoveArmyInternal),
            __instance,
            army,
            new ArmyListRemoved(__instance, army),
            __instance._armies?.Contains(army) == true);
    }

    [HarmonyPatch(nameof(Kingdom.AddClanInternal))]
    [HarmonyPrefix]
    private static bool AddClanPrefix(Kingdom __instance, Clan clan)
    {
        return PublishSingleChange(
            nameof(Kingdom.AddClanInternal),
            __instance,
            clan,
            new ClanListUpdated(__instance, clan),
            __instance._clans?.Contains(clan) != true);
    }

    [HarmonyPatch(nameof(Kingdom.RemoveClanInternal))]
    [HarmonyPrefix]
    private static bool RemoveClanPrefix(Kingdom __instance, Clan clan)
    {
        return PublishSingleChange(
            nameof(Kingdom.RemoveClanInternal),
            __instance,
            clan,
            new ClanListRemoved(__instance, clan),
            __instance._clans?.Contains(clan) == true);
    }

    [HarmonyPatch(nameof(Kingdom.OnFortificationAdded))]
    [HarmonyPrefix]
    private static bool OnFortificationAddedPrefix(Kingdom __instance, Town fortification)
    {
        if (!CanPublishServerChange(nameof(Kingdom.OnFortificationAdded), out var allowOriginal)) return allowOriginal;

        PublishIfChanging(
            __instance,
            fortification,
            new FiefsCacheUpdated(__instance, fortification),
            __instance._fiefsCache?.Contains(fortification) != true);

        if (fortification?.Settlement != null)
        {
            PublishIfChanging(
                __instance,
                fortification.Settlement,
                new SettlementsCacheUpdated(__instance, fortification.Settlement),
                __instance._settlementsCache?.Contains(fortification.Settlement) != true);
        }

        return true;
    }

    [HarmonyPatch(nameof(Kingdom.OnFortificationRemoved))]
    [HarmonyPrefix]
    private static bool OnFortificationRemovedPrefix(Kingdom __instance, Town fortification)
    {
        if (!CanPublishServerChange(nameof(Kingdom.OnFortificationRemoved), out var allowOriginal)) return allowOriginal;

        PublishIfChanging(
            __instance,
            fortification,
            new FiefsCacheRemoved(__instance, fortification),
            __instance._fiefsCache?.Contains(fortification) == true);

        if (fortification?.Settlement != null)
        {
            PublishIfChanging(
                __instance,
                fortification.Settlement,
                new SettlementsCacheRemoved(__instance, fortification.Settlement),
                __instance._settlementsCache?.Contains(fortification.Settlement) == true);
        }

        return true;
    }

    [HarmonyPatch(nameof(Kingdom.OnBoundVillageAdded))]
    [HarmonyPrefix]
    private static bool OnBoundVillageAddedPrefix(Kingdom __instance, Village village)
    {
        if (!CanPublishServerChange(nameof(Kingdom.OnBoundVillageAdded), out var allowOriginal)) return allowOriginal;

        PublishIfChanging(
            __instance,
            village,
            new VillagesCacheUpdated(__instance, village),
            __instance._villagesCache?.Contains(village) != true);

        if (village?.Settlement != null)
        {
            PublishIfChanging(
                __instance,
                village.Settlement,
                new SettlementsCacheUpdated(__instance, village.Settlement),
                __instance._settlementsCache?.Contains(village.Settlement) != true);
        }

        return true;
    }

    [HarmonyPatch(nameof(Kingdom.OnBoundVillageRemoved))]
    [HarmonyPrefix]
    private static bool OnBoundVillageRemovedPrefix(Kingdom __instance, Village village)
    {
        if (!CanPublishServerChange(nameof(Kingdom.OnBoundVillageRemoved), out var allowOriginal)) return allowOriginal;

        PublishIfChanging(
            __instance,
            village,
            new VillagesCacheRemoved(__instance, village),
            __instance._villagesCache?.Contains(village) == true);

        if (village?.Settlement != null)
        {
            PublishIfChanging(
                __instance,
                village.Settlement,
                new SettlementsCacheRemoved(__instance, village.Settlement),
                __instance._settlementsCache?.Contains(village.Settlement) == true);
        }

        return true;
    }

    [HarmonyPatch(nameof(Kingdom.OnHeroAdded))]
    [HarmonyPrefix]
    private static bool OnHeroAddedPrefix(Kingdom __instance, Hero hero)
    {
        return PublishSingleChange(
            nameof(Kingdom.OnHeroAdded),
            __instance,
            hero,
            new HeroesCacheUpdated(__instance, hero),
            __instance._heroesCache?.Contains(hero) != true);
    }

    [HarmonyPatch(nameof(Kingdom.OnHeroRemoved))]
    [HarmonyPrefix]
    private static bool OnHeroRemovedPrefix(Kingdom __instance, Hero hero)
    {
        return PublishSingleChange(
            nameof(Kingdom.OnHeroRemoved),
            __instance,
            hero,
            new HeroesCacheRemoved(__instance, hero),
            __instance._heroesCache?.Contains(hero) == true);
    }

    [HarmonyPatch(nameof(Kingdom.OnLordAdded))]
    [HarmonyPrefix]
    private static bool OnLordAddedPrefix(Kingdom __instance, Hero hero)
    {
        return PublishLordChange(__instance, hero, isAdd: true);
    }

    [HarmonyPatch(nameof(Kingdom.OnLordRemoved))]
    [HarmonyPrefix]
    private static bool OnLordRemovedPrefix(Kingdom __instance, Hero hero)
    {
        return PublishLordChange(__instance, hero, isAdd: false);
    }

    [HarmonyPatch(nameof(Kingdom.OnWarPartyAdded))]
    [HarmonyPrefix]
    private static bool OnWarPartyAddedPrefix(Kingdom __instance, WarPartyComponent warPartyComponent)
    {
        return PublishSingleChange(
            nameof(Kingdom.OnWarPartyAdded),
            __instance,
            warPartyComponent,
            new WarPartyComponentsCacheUpdated(__instance, warPartyComponent),
            __instance._warPartyComponentsCache?.Contains(warPartyComponent) != true);
    }

    [HarmonyPatch(nameof(Kingdom.OnWarPartyRemoved))]
    [HarmonyPrefix]
    private static bool OnWarPartyRemovedPrefix(Kingdom __instance, WarPartyComponent warPartyComponent)
    {
        return PublishSingleChange(
            nameof(Kingdom.OnWarPartyRemoved),
            __instance,
            warPartyComponent,
            new WarPartyComponentsCacheRemoved(__instance, warPartyComponent),
            __instance._warPartyComponentsCache?.Contains(warPartyComponent) == true);
    }

    private static bool PublishLordChange(Kingdom kingdom, Hero hero, bool isAdd)
    {
        if (!CanPublishServerChange(isAdd ? nameof(Kingdom.OnLordAdded) : nameof(Kingdom.OnLordRemoved), out var allowOriginal))
        {
            return allowOriginal;
        }

        var isDead = hero?.IsDead == true;
        var contains = isDead
            ? kingdom._deadLordsCache?.Contains(hero) == true
            : kingdom._aliveLordsCache?.Contains(hero) == true;

        bool changed = isAdd ? !contains : contains;
        if (isDead)
        {
            // Split ternary to preserve concrete type for generic type inference in PublishIfChanging
            if (isAdd)
                PublishIfChanging(kingdom, hero, new DeadLordsCacheUpdated(kingdom, hero), changed);
            else
                PublishIfChanging(kingdom, hero, new DeadLordsCacheRemoved(kingdom, hero), changed);
        }
        else
        {
            // Split ternary to preserve concrete type for generic type inference in PublishIfChanging
        if (isAdd)
                PublishIfChanging(kingdom, hero, new AliveLordsCacheUpdated(kingdom, hero), changed);
            else
                PublishIfChanging(kingdom, hero, new AliveLordsCacheRemoved(kingdom, hero), changed);
        }

        return true;
    }

    // Generic parameter TMessage preserves the concrete type(e.g.ArmyListUpdated) all the way
    // through to MessageBroker.Publish<T>, so T is inferred as ArmyListUpdated not IEvent.
    // Without this, message typed as IEvent causes Publish to store under subscribers[typeof(IEvent)]
    // which never matches handlers subscribed to subscribers[typeof(ArmyListUpdated)].
    private static bool PublishSingleChange<TMessage>(
    string memberName,
    Kingdom kingdom,
    object value,
    TMessage message,
    bool changed)
    where TMessage : IEvent
    {
        if (!CanPublishServerChange(memberName, out var allowOriginal)) return allowOriginal;
        PublishIfChanging(kingdom, value, message, changed);
        return true;
    }

    // TMessage preserved here for the same reason, if this accepted IEvent instead,
    // the Publish call below would infer T = IEvent regardless of the concrete type passed in.
    private static void PublishIfChanging<TMessage>(Kingdom kingdom, object value, TMessage message, bool changed)
        where TMessage : IEvent
    {
        if (changed && value != null)
        {
            MessageBroker.Instance.Publish(kingdom, message);
        }
    }

    private static bool CanPublishServerChange(string memberName, out bool allowOriginal)
    {
        allowOriginal = true;

        if (CallOriginalPolicy.IsOriginalAllowed()) return false;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client attempted to mutate server-owned Kingdom collection through {MemberName}", memberName);
            allowOriginal = false;
            return false;
        }

        return true;
    }
}
