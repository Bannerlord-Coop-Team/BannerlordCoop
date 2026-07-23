using Common;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs.Patches;

[HarmonyPatch]
internal class AiEngagePartyBehaviorPatches
{
    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(AiEngagePartyBehavior), nameof(AiEngagePartyBehavior.RegisterEvents)),
        AccessTools.Method(typeof(AiMilitaryBehavior), nameof(AiMilitaryBehavior.RegisterEvents)),
        AccessTools.Method(typeof(AiPartyThinkBehavior), nameof(AiPartyThinkBehavior.RegisterEvents)),
        AccessTools.Method(typeof(AiVisitSettlementBehavior), nameof(AiVisitSettlementBehavior.RegisterEvents))
    };

    static bool Prefix() => ModInformation.IsServer;
}

/// <summary>
/// Player parties are client-authoritative and driven by player input. Skip the server's autonomous
/// per-party AI decision tick for them so it never orders a player party to move on its own
/// (e.g. GoToSettlement). NPC parties are unaffected, and this behavior is already disabled on clients.
/// </summary>
[HarmonyPatch(typeof(AiPartyThinkBehavior), nameof(AiPartyThinkBehavior.PartyHourlyAiTick))]
internal class SkipPlayerPartyAiThinkPatch
{
    static bool Prefix(MobileParty mobileParty) => mobileParty == null || !mobileParty.IsPlayerParty();
}