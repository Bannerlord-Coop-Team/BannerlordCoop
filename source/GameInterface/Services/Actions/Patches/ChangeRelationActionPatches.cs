using Common;
using Common.Logging;
using GameInterface.Services.Heroes.Patches;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Actions.Patches;

[HarmonyPatch(typeof(ChangeRelationAction))]
internal class ChangeRelationActionPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<ChangeRelationActionPatches>();

    [HarmonyPatch(nameof(ChangeRelationAction.ApplyInternal))]
    static bool Prefix() => ModInformation.IsServer;

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
