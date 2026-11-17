using Common;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

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
/// (e.g. GoToSettlement). Also skip AI parties in a player's MapEvent, otherwise a siege or raid attacker
/// leader can pick a new target mid-battle and finalize the event under the player.
/// This behavior is already disabled on clients.
/// </summary>
[HarmonyPatch(typeof(AiPartyThinkBehavior), nameof(AiPartyThinkBehavior.PartyHourlyAiTick))]
internal class SkipPlayerPartyAiThinkPatch
{
    static bool Prefix(MobileParty mobileParty) =>
        mobileParty == null || (!mobileParty.IsPlayerParty() && !mobileParty.MapEvent.ContainsPlayerParty());
}

/// <summary>
/// Drops hourly engage targets inside a player battle closed to AI joins.
/// Vanilla scores as GoAroundParty and only switches to EngageParty near the target.
/// </summary>
[HarmonyPatch(typeof(AiEngagePartyBehavior))]
internal class SkipClosedPlayerBattleEngagePatch
{
    [HarmonyPatch(nameof(AiEngagePartyBehavior.AiHourlyTick))]
    [HarmonyPostfix]
    public static void AiHourlyTickPostfix(PartyThinkParams p)
    {
        p._aiBehaviorScores.RemoveAll(score =>
            score.Item1.AiBehavior == AiBehavior.GoAroundParty &&
            score.Item1.Party is MobileParty target &&
            InteractionPatches.IsClosedToAiJoins(target.MapEvent));
    }
}

/// <summary>
/// Drops hourly besiege and defend targets whose siege battle is closed to AI joins, otherwise the party walks up
/// and gets held beside it. Parties already in that siege's camp keep their score so they aren't ejected.
/// </summary>
[HarmonyPatch(typeof(AiMilitaryBehavior))]
internal class SkipClosedSiegeBattleMilitaryPatch
{
    [HarmonyPatch(nameof(AiMilitaryBehavior.AiHourlyTick))]
    [HarmonyPostfix]
    public static void AiHourlyTickPostfix(MobileParty mobileParty, PartyThinkParams p)
    {
        p._aiBehaviorScores.RemoveAll(score =>
            (score.Item1.AiBehavior == AiBehavior.BesiegeSettlement || score.Item1.AiBehavior == AiBehavior.DefendSettlement) &&
            score.Item1.Party is Settlement target &&
            mobileParty.BesiegedSettlement != target &&
            InteractionPatches.IsSiegeClosedToAiJoins(target));
    }
}