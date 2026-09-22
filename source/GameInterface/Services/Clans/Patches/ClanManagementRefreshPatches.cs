using Common;
using Common.Messaging;
using GameInterface.Services.Clans.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Clans.Patches;

/// <summary>
/// A variety of events change data viewed in the clan management screen.
/// In vanilla, these never cause a refresh because time is always paused with ClanManagementVM open.
/// </summary>
[HarmonyPatch]
internal static class ClanManagementRefreshPatches
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnHeroChangedClan))]
    [HarmonyPostfix]
    public static void HeroChangedClanPostfix(Hero hero, Clan oldClan)
    {
        Refresh(oldClan, ClanManagementRefresh.All);
        Refresh(hero.Clan, ClanManagementRefresh.All);
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnHeroCreated))]
    [HarmonyPostfix]
    public static void HeroCreatedPostfix(Hero hero) => RefreshMember(hero);

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnHeroKilled))]
    [HarmonyPostfix]
    public static void HeroKilledPostfix(Hero victim) => RefreshMember(victim);

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnHeroPrisonerReleased))]
    [HarmonyPostfix]
    public static void HeroPrisonerReleasedPostfix(Hero prisoner) => RefreshMember(prisoner);

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnHeroComesOfAge))]
    [HarmonyPostfix]
    public static void HeroComesOfAgePostfix(Hero hero) => RefreshMember(hero);

    [HarmonyPatch(typeof(Clan), nameof(Clan.OnCompanionAdded))]
    [HarmonyPostfix]
    public static void CompanionAddedPostfix(Clan __instance) =>
        Refresh(__instance, ClanManagementRefresh.Members | ClanManagementRefresh.Parties);

    [HarmonyPatch(typeof(Clan), nameof(Clan.OnCompanionRemoved))]
    [HarmonyPostfix]
    public static void CompanionRemovedPostfix(Clan __instance) =>
        Refresh(__instance, ClanManagementRefresh.Members | ClanManagementRefresh.Parties);

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnHeroPrisonerTaken))]
    [HarmonyPostfix]
    public static void HeroPrisonerTakenPostfix(Hero prisoner) => RefreshMember(prisoner);

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnBeforeHeroesMarried))]
    [HarmonyPostfix]
    public static void HeroesMarriedPostfix(Hero hero1, Hero hero2)
    {
        Refresh(hero1.Clan, ClanManagementRefresh.Members | ClanManagementRefresh.Parties);
        Refresh(hero2.Clan, ClanManagementRefresh.Members | ClanManagementRefresh.Parties);
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMobilePartyDestroyed))]
    [HarmonyPrefix]
    public static void MobilePartyDestroyedPrefix(MobileParty mobileParty) =>
        Refresh(mobileParty.ActualClan, ClanManagementRefresh.Parties | ClanManagementRefresh.Members);

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnClanLeaderChanged))]
    [HarmonyPostfix]
    public static void ClanLeaderChangedPostfix(Hero oldLeader, Hero newLeader)
    {
        Refresh(oldLeader?.Clan, ClanManagementRefresh.All);
        Refresh(newLeader.Clan, ClanManagementRefresh.All);
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnClanChangedKingdom))]
    [HarmonyPostfix]
    public static void ClanChangedKingdomPostfix(Clan clan)
    {
        Refresh(clan, ClanManagementRefresh.All);
    }

    [HarmonyPatch(typeof(Clan), nameof(Clan.OnSupporterNotableAdded))]
    [HarmonyPostfix]
    public static void SupporterAddedPostfix(Clan __instance) => Refresh(__instance, ClanManagementRefresh.Income);

    [HarmonyPatch(typeof(Clan), nameof(Clan.OnSupporterNotableRemoved))]
    [HarmonyPostfix]
    public static void SupporterRemovedPostfix(Clan __instance) => Refresh(__instance, ClanManagementRefresh.Income);

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnWorkshopOwnerChanged))]
    [HarmonyPostfix]
    public static void WorkshopOwnerChangedPostfix(Workshop workshop, Hero oldOwner)
    {
        Refresh(oldOwner?.Clan, ClanManagementRefresh.Income);
        Refresh(workshop.Owner?.Clan, ClanManagementRefresh.Income);
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnAlleyOwnerChanged))]
    [HarmonyPostfix]
    public static void AlleyOwnerChangedPostfix(Hero newOwner, Hero oldOwner)
    {
        Refresh(oldOwner?.Clan, ClanManagementRefresh.Income | ClanManagementRefresh.Members);
        Refresh(newOwner?.Clan, ClanManagementRefresh.Income | ClanManagementRefresh.Members);
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.DailyTickClan))]
    [HarmonyPostfix]
    public static void DailyTickClanPostfix(Clan clan)
    {
        Refresh(clan, ClanManagementRefresh.Finances);
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnHeroOrPartyTradedGold))]
    [HarmonyPostfix]
    public static void GoldChangedPostfix((Hero, PartyBase) giver, (Hero, PartyBase) recipient)
    {
        Refresh(giver.Item1?.Clan ?? giver.Item2?.Owner?.Clan, ClanManagementRefresh.Finances);
        Refresh(recipient.Item1?.Clan ?? recipient.Item2?.Owner?.Clan, ClanManagementRefresh.Finances);
    }

    private static void RefreshMember(Hero hero)
    {
        Refresh(hero.Clan, ClanManagementRefresh.Members | ClanManagementRefresh.Parties);
    }

    private static void Refresh(Clan clan, ClanManagementRefresh sections)
    {
        if (ModInformation.IsServer && clan != null)
            MessageBroker.Instance.Publish(null, new ClanManagementChanged(clan, sections));
    }
}
