using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Heroes.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(Clan))]
internal class ClanPatches
{
    [HarmonyPatch(nameof(Clan.CreateSettlementRebelClan))]
    [HarmonyPostfix]
    internal static void CreateSettlementRebelClanPostfix(Clan __result)
    {
        if (ModInformation.IsServer)
        {
            MessageBroker.Instance.Publish(__result, new SettlementRebelClanInitialized(__result));
        }
    }

    [HarmonyPatch(nameof(Clan.PlayerClan))]
    [HarmonyPatch(MethodType.Getter)]
    [HarmonyPrefix]
    static bool PlayerClanGetter()
    {
        if (Campaign.Current == null) return false;
        
        return true;
    }

    // Patch for server to use passed down ClientHero instead of server's MainHero
    // which leads to a different hero
    [HarmonyPatch(nameof(Clan.CreateCompanionToLordClan))]
    [HarmonyPrefix]
    public static bool CreateCompanionToLordClanPrefix(Hero hero, Settlement settlement, TextObject clanName, int newClanIconId, ref Clan __result)
    {
        Clan clan = Clan.CreateClan(ResolvedMainHeroContext.ResolvedMainHero.MapFaction.StringId + "_companion_clan");
        clan.ChangeClanName(clanName, clanName);
        clan.Culture = settlement.Culture;
        clan.Banner = Banner.CreateOneColoredBannerWithOneIcon(settlement.MapFaction.Banner.GetFirstIconColor(), settlement.MapFaction.Banner.GetPrimaryColor(), newClanIconId);
        clan.Kingdom = ResolvedMainHeroContext.ResolvedMainHero.Clan.Kingdom;
        clan.Tier = Campaign.Current.Models.ClanTierModel.CompanionToLordClanStartingTier;
        clan.SetInitialHomeSettlement(settlement);
        hero.Clan = clan;
        clan.SetLeader(hero);
        clan.IsNoble = true;
        ChangeOwnerOfSettlementAction.ApplyByGift(settlement, hero);
        CampaignEventDispatcher.Instance.OnClanCreated(clan, true);
        __result = clan;
        return false;
    }

    [HarmonyPatch(nameof(Clan.SetKingdomInternal))]
    [HarmonyPrefix]
    public static bool KingdomSetterPrefix(Clan __instance, Kingdom value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsClient) return false;
        if (__instance.Kingdom == value) return false;
        var message = new SetClanKingdom(__instance, value);
        MessageBroker.Instance.Publish(__instance, message);
        return true;
    }

    [HarmonyPatch(nameof(Clan.OnSupportedByClan))]
    [HarmonyPrefix]
    private static bool OnSupportedByClanPrefix(Clan __instance, Clan supporterClan)
    {
        DiplomacyModel diplomacyModel = Campaign.Current.Models.DiplomacyModel;
        int influenceCostOfSupportingClan = diplomacyModel.GetInfluenceCostOfSupportingClan();
        if (supporterClan.Influence >= (float)influenceCostOfSupportingClan)
        {
            MessageBroker.Instance.Publish(__instance, new OnClanSupported(supporterClan, __instance));
        }
        return false;
    }

    [HarmonyPatch(nameof(Clan.UpdateBannerColorsAccordingToKingdom))]
    [HarmonyPostfix]
    public static void UpdateBannerColorsAccordingToKingdomPostfix(Clan __instance)
    {
        if (ModInformation.IsClient) return;

        var message = new UpdateBannerColorsOfClan(__instance);
        MessageBroker.Instance.Publish(__instance, message);
    }
}
