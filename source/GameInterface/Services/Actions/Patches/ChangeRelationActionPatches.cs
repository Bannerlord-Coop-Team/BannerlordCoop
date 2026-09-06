using Common;
using Common.Logging;
using GameInterface.Services.Heroes.Patches;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.ComponentInterfaces;

namespace GameInterface.Services.Actions.Patches;

[HarmonyPatch(typeof(ChangeRelationAction))]
internal class ChangeRelationActionPatches
{
    private const int InvalidEffectiveHeroPairLogLimit = 256;

    private static readonly ILogger Logger = LogManager.GetLogger<ChangeRelationActionPatches>();
    private static readonly HashSet<string> LoggedInvalidEffectiveHeroPairs = new HashSet<string>();
    private static readonly object LoggedInvalidEffectiveHeroPairsLock = new object();

    [HarmonyPatch(nameof(ChangeRelationAction.ApplyInternal), new[]
    {
        typeof(Hero),
        typeof(Hero),
        typeof(int),
        typeof(bool),
        typeof(ChangeRelationAction.ChangeRelationDetail),
    })]
    [HarmonyPrefix]
    internal static bool ApplyInternalPrefix(
        Hero originalHero,
        Hero originalGainedRelationWith,
        int relationChange)
    {
        if (ModInformation.IsClient)
        {
            return false;
        }

        // Native ApplyInternal does not inspect either hero for a no-op relation
        // change, so preserve that behavior even if campaign state is incomplete.
        if (relationChange == 0)
        {
            return true;
        }

        DiplomacyModel diplomacyModel = Campaign.Current?.Models?.DiplomacyModel;
        if (TryResolveEffectiveHeroes(
            diplomacyModel,
            originalHero,
            originalGainedRelationWith,
            out Hero effectiveHero,
            out Hero effectiveGainedRelationWith))
        {
            return true;
        }

        string sourceHeroId = originalHero?.StringId ?? "<null>";
        string sourceClanId = originalHero?.Clan?.StringId ?? "<none>";
        string targetHeroId = originalGainedRelationWith?.StringId ?? "<null>";
        string targetClanId = originalGainedRelationWith?.Clan?.StringId ?? "<none>";
        string pairKey = $"{sourceHeroId}|{sourceClanId}|{targetHeroId}|{targetClanId}";

        if (ShouldLogInvalidEffectiveHeroPair(pairKey))
        {
            Logger.Warning(
                "Skipped relation change because its effective hero pair could not be resolved: " +
                "sourceHero={SourceHeroId}, sourceClan={SourceClanId}, targetHero={TargetHeroId}, targetClan={TargetClanId}, " +
                "effectiveSource={EffectiveSourceHeroId}, effectiveTarget={EffectiveTargetHeroId}",
                sourceHeroId,
                sourceClanId,
                targetHeroId,
                targetClanId,
                effectiveHero?.StringId ?? "<null>",
                effectiveGainedRelationWith?.StringId ?? "<null>");
        }

        return false;
    }

    private static bool ShouldLogInvalidEffectiveHeroPair(string pairKey)
    {
        lock (LoggedInvalidEffectiveHeroPairsLock)
        {
            if (LoggedInvalidEffectiveHeroPairs.Count >= InvalidEffectiveHeroPairLogLimit)
            {
                return false;
            }

            return LoggedInvalidEffectiveHeroPairs.Add(pairKey);
        }
    }

    internal static bool TryResolveEffectiveHeroes(
        DiplomacyModel diplomacyModel,
        Hero originalHero,
        Hero originalGainedRelationWith,
        out Hero effectiveHero,
        out Hero effectiveGainedRelationWith)
    {
        effectiveHero = null;
        effectiveGainedRelationWith = null;

        if (diplomacyModel == null || originalHero == null || originalGainedRelationWith == null)
        {
            return false;
        }

        diplomacyModel.GetHeroesForEffectiveRelation(
            originalHero,
            originalGainedRelationWith,
            out effectiveHero,
            out effectiveGainedRelationWith);

        return effectiveHero != null && effectiveGainedRelationWith != null;
    }

    // Patch for server to use passed down ClientHero instead of server's MainHero
    // which is a different hero
    [HarmonyPatch(nameof(ChangeRelationAction.ApplyPlayerRelation))]
    [HarmonyPrefix]
    public static bool ApplyPlayerRelationPrefix(Hero gainedRelationWith, int relation, bool affectRelatives = true, bool showQuickNotification = true)
    {
        ChangeRelationAction.ApplyInternal(ResolvedMainHeroContext.ResolvedMainHero, gainedRelationWith, relation, showQuickNotification, ChangeRelationAction.ChangeRelationDetail.Default);
        return false;
    }
}
