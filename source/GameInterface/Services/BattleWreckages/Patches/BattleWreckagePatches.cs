using Common;
using Common.Messaging;
using GameInterface.Services.BattleWreckages.Messages;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BattleWreckages;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.BattleWreckages.Patches;

[HarmonyPatch(typeof(BattleWreckage))]
internal class BattleWreckagePatches
{
    [HarmonyPatch(nameof(BattleWreckage.OnPartyInteraction))]
    [HarmonyPrefix]
    public static bool OnPartyInteractionPrefix(BattleWreckage __instance, MobileParty mobileParty)
    {
        if (ModInformation.IsServer) return false;

        // Stop interacting so the request isn't sent every tick until the server replies
        mobileParty.Ai.AiBehaviorInteractable = null;

        var message = new RequestStartWreckageEncounter(__instance, mobileParty.LeaderHero);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    /// <summary>
    /// Replicated to clients by BattleWreckageRegistry
    /// </summary>
    [HarmonyPatch(nameof(BattleWreckage.DestroyWreckage))]
    [HarmonyPrefix]
    public static bool DestroyWreckagePrefix() => ModInformation.IsServer;

    /// <summary>
    /// Reads Hero.MainHero, which the server doesn't have, and visibility only matters on clients
    /// </summary>
    [HarmonyPatch(nameof(BattleWreckage.UpdateVisibility))]
    [HarmonyPrefix]
    public static bool UpdateVisibilityPrefix() => ModInformation.IsClient;

    /// <summary>
    /// Vanilla falls back to the settlement closest to MobileParty.MainParty, which the server doesn't have
    /// </summary>
    [HarmonyPatch(nameof(BattleWreckage.GetNameOfWreckage))]
    [HarmonyPrefix]
    public static bool GetNameOfWreckagePrefix(BattleWreckage __instance, ref TextObject __result)
    {
        if (ModInformation.IsClient) return true;

        // Same checks vanilla makes before it reaches the fallback
        if (__instance.AttackerLeaderHero == null || __instance.DefenderLeaderHero == null) return true;
        if (__instance.WreckageTypeCategory == BattleWreckage.WreckageType.Small) return true;

        bool isOnLand = __instance.Position.IsOnLand;
        var navigationType = isOnLand ? MobileParty.NavigationType.Default : MobileParty.NavigationType.Naval;
        if (Campaign.Current.Models.MapDistanceModel.GetClosestEntranceToFace(__instance.Position.Face, navigationType).Item1 != null) return true;

        var settlement = Campaign.Current.Settlements
            .Where(x => isOnLand || x.HasPort)
            .MinBy(x => x.Position.Distance(__instance.Position));

        __result = isOnLand
            ? new TextObject("{=Um9djvw8}Aftermath of the battle of {CLOSEST_SETTLEMENT_NAME}")
            : new TextObject("{=DStYh5rg}Aftermath of the naval battle of {CLOSEST_SETTLEMENT_NAME}");
        __result.SetTextVariable("CLOSEST_SETTLEMENT_NAME", settlement.Name);

        return false;
    }
}
