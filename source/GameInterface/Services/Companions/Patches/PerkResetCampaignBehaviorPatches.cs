using Common;
using Common.Messaging;
using GameInterface.Services.Companions.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Companions.Patches;

[HarmonyPatch(typeof(PerkResetCampaignBehavior))]
internal class PerkResetCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(PerkResetCampaignBehavior.OnPerkReset))]
    [HarmonyPrefix]
    public static bool OnPerkResetPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(PerkResetCampaignBehavior.ResetPerkTreeForHero))]
    [HarmonyPrefix]
    public static bool ResetPerkTreeForHeroPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(PerkResetCampaignBehavior.ClearPerksForSkill))]
    [HarmonyPrefix]
    public static bool ClearPerksForSkillPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(PerkResetCampaignBehavior.DailyTick))]
    [HarmonyPrefix]
    public static bool DailyTickPrefix()
    {
        // Server has no main hero/player clan
        // Manage warning time client side and broadcast result to remove companion on server
        // If the client isn't the clan leader, don't run the tick to avoid duplication
        return ModInformation.IsClient && Hero.MainHero.IsClanLeader;
    }

    [HarmonyPatch(nameof(PerkResetCampaignBehavior.DailyTick))]
    [HarmonyPostfix]
    public static void DailyTickPostfix(PerkResetCampaignBehavior __instance)
    {
        if (ModInformation.IsServer) return;

        var message = new UpdateCompanionWarningTime(Hero.MainHero, __instance._warningTime._numTicks);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(PerkResetCampaignBehavior.conversation_arena_player_accept_perk_reset_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationArenaPlayerAcceptPerkResetOnConsequencePrefix(PerkResetCampaignBehavior __instance)
    {
        var message = new ResetPerksByArenaMaster(
            Hero.MainHero,
            __instance.PerkResetCost,
            __instance._heroForPerkReset,
            __instance._selectedSkillForReset);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(PerkResetCampaignBehavior.RemoveACompanionFromPlayerParty))]
    [HarmonyPrefix]
    public static bool RemoveACompanionFromPlayerPartyPrefix(PerkResetCampaignBehavior __instance)
    {
        if (ModInformation.IsServer) return false;

        var message = new RemoveACompanionFromPlayerParty(Clan.PlayerClan);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }
}