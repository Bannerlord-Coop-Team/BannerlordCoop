#if DEBUG
using Common;
using Common.Logging;
using HarmonyLib;
using SandBox;
using Serilog;
using System;
using System.Reflection;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.CampaignService.Patches;

internal static class CampaignLoadPhaseDiagnosticPatch
{
    private const string DedicatedServerHostTypeName = "DedicatedServer.CoopServerHost";

    internal static void RecordStarted(string phase)
    {
        if (!ContainerProvider.TryResolve<ICampaignLoadPhaseDiagnostic>(out var diagnostic))
        {
            Logger.Error("Unable to resolve {Diagnostic}", nameof(ICampaignLoadPhaseDiagnostic));
            return;
        }

        diagnostic.RecordStarted(phase);
    }

    internal static void RecordCompleted(string phase)
    {
        if (!ContainerProvider.TryResolve<ICampaignLoadPhaseDiagnostic>(out var diagnostic))
        {
            Logger.Error("Unable to resolve {Diagnostic}", nameof(ICampaignLoadPhaseDiagnostic));
            return;
        }

        diagnostic.RecordCompleted(phase);
    }

    internal static Type GetDedicatedServerHostType() => AccessTools.TypeByName(DedicatedServerHostTypeName);

    internal static bool IsDedicatedServerServing()
    {
        object phase = AccessTools.Field(GetDedicatedServerHostType(), "_phase")?.GetValue(null);
        return string.Equals(phase?.ToString(), "Serving", StringComparison.Ordinal);
    }

    internal static string GetDedicatedServerHostState(bool includeQueue = true)
    {
        Type hostType = GetDedicatedServerHostType();
        object phase = AccessTools.Field(hostType, "_phase")?.GetValue(null);
        object ticks = AccessTools.Field(hostType, "_ticks")?.GetValue(null);
        GameManagerBase manager = GameManagerBase.Current;
        MBGameManager gameManager = manager as MBGameManager;
        string queue = includeQueue ? GameThread.Instance.QueueLength.ToString() : "not-sampled";

        return $"phase={phase ?? "<unknown>"}|ticks={ticks ?? "<unknown>"}|" +
            $"manager={manager?.GetType().FullName ?? "<null>"}|isLoaded={gameManager?.IsLoaded.ToString() ?? "<null>"}|" +
            $"campaign={(Campaign.Current != null)}|queue={queue}";
    }

    private static readonly ILogger Logger = LogManager.GetLogger(typeof(CampaignLoadPhaseDiagnosticPatch));
}

[HarmonyPatch(typeof(MBObjectManager), nameof(MBObjectManager.PreAfterLoad))]
internal static class ObjectManagerPreAfterLoadDiagnosticPatch
{
    [HarmonyPrefix]
    private static void Prefix() => CampaignLoadPhaseDiagnosticPatch.RecordStarted("ObjectManager.PreAfterLoad");

    [HarmonyPostfix]
    private static void Postfix() => CampaignLoadPhaseDiagnosticPatch.RecordCompleted("ObjectManager.PreAfterLoad");
}

[HarmonyPatch(typeof(CampaignObjectManager), nameof(CampaignObjectManager.PreAfterLoad))]
internal static class CampaignObjectManagerPreAfterLoadDiagnosticPatch
{
    [HarmonyPrefix]
    private static void Prefix() => CampaignLoadPhaseDiagnosticPatch.RecordStarted("CampaignObjectManager.PreAfterLoad");

    [HarmonyPostfix]
    private static void Postfix() => CampaignLoadPhaseDiagnosticPatch.RecordCompleted("CampaignObjectManager.PreAfterLoad");
}

[HarmonyPatch(typeof(IssueManager), nameof(IssueManager.PreAfterLoad))]
internal static class IssueManagerPreAfterLoadDiagnosticPatch
{
    [HarmonyPrefix]
    private static void Prefix() => CampaignLoadPhaseDiagnosticPatch.RecordStarted("IssueManager.PreAfterLoad");

    [HarmonyPostfix]
    private static void Postfix() => CampaignLoadPhaseDiagnosticPatch.RecordCompleted("IssueManager.PreAfterLoad");
}

[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.PreAfterLoad))]
internal static class QuestManagerPreAfterLoadDiagnosticPatch
{
    [HarmonyPrefix]
    private static void Prefix() => CampaignLoadPhaseDiagnosticPatch.RecordStarted("QuestManager.PreAfterLoad");

    [HarmonyPostfix]
    private static void Postfix() => CampaignLoadPhaseDiagnosticPatch.RecordCompleted("QuestManager.PreAfterLoad");
}

[HarmonyPatch(typeof(MBObjectManager), nameof(MBObjectManager.AfterLoad))]
internal static class ObjectManagerAfterLoadDiagnosticPatch
{
    [HarmonyPrefix]
    private static void Prefix() => CampaignLoadPhaseDiagnosticPatch.RecordStarted("ObjectManager.AfterLoad");

    [HarmonyPostfix]
    private static void Postfix() => CampaignLoadPhaseDiagnosticPatch.RecordCompleted("ObjectManager.AfterLoad");
}

[HarmonyPatch(typeof(CampaignObjectManager), nameof(CampaignObjectManager.AfterLoad))]
internal static class CampaignObjectManagerAfterLoadDiagnosticPatch
{
    [HarmonyPrefix]
    private static void Prefix() => CampaignLoadPhaseDiagnosticPatch.RecordStarted("CampaignObjectManager.AfterLoad");

    [HarmonyPostfix]
    private static void Postfix() => CampaignLoadPhaseDiagnosticPatch.RecordCompleted("CampaignObjectManager.AfterLoad");
}

[HarmonyPatch(typeof(CharacterRelationManager), nameof(CharacterRelationManager.AfterLoad))]
internal static class CharacterRelationManagerAfterLoadDiagnosticPatch
{
    [HarmonyPrefix]
    private static void Prefix() => CampaignLoadPhaseDiagnosticPatch.RecordStarted("CharacterRelationManager.AfterLoad");

    [HarmonyPostfix]
    private static void Postfix() => CampaignLoadPhaseDiagnosticPatch.RecordCompleted("CharacterRelationManager.AfterLoad");
}

[HarmonyPatch(typeof(FactionManager), nameof(FactionManager.AfterLoad))]
internal static class FactionManagerAfterLoadDiagnosticPatch
{
    [HarmonyPrefix]
    private static void Prefix() => CampaignLoadPhaseDiagnosticPatch.RecordStarted("FactionManager.AfterLoad");

    [HarmonyPostfix]
    private static void Postfix() => CampaignLoadPhaseDiagnosticPatch.RecordCompleted("FactionManager.AfterLoad");
}

[HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnGameEarlyLoaded))]
internal static class GameEarlyLoadedDiagnosticPatch
{
    [HarmonyPrefix]
    private static void Prefix() => CampaignLoadPhaseDiagnosticPatch.RecordStarted("CampaignEventDispatcher.OnGameEarlyLoaded");

    [HarmonyPostfix]
    private static void Postfix() => CampaignLoadPhaseDiagnosticPatch.RecordCompleted("CampaignEventDispatcher.OnGameEarlyLoaded");
}

[HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnGameLoaded))]
internal static class GameLoadedDiagnosticPatch
{
    [HarmonyPrefix]
    private static void Prefix() => CampaignLoadPhaseDiagnosticPatch.RecordStarted("CampaignEventDispatcher.OnGameLoaded");

    [HarmonyPostfix]
    private static void Postfix() => CampaignLoadPhaseDiagnosticPatch.RecordCompleted("CampaignEventDispatcher.OnGameLoaded");
}

[HarmonyPatch(typeof(Campaign), nameof(Campaign.InitializeForSavedGame))]
internal static class InitializeForSavedGameDiagnosticPatch
{
    [HarmonyPrefix]
    private static void Prefix() => CampaignLoadPhaseDiagnosticPatch.RecordStarted("Campaign.InitializeForSavedGame");

    [HarmonyPostfix]
    private static void Postfix() => CampaignLoadPhaseDiagnosticPatch.RecordCompleted("Campaign.InitializeForSavedGame");
}

[HarmonyPatch(typeof(Campaign), nameof(Campaign.OnSessionStart))]
internal static class OnSessionStartDiagnosticPatch
{
    [HarmonyPrefix]
    private static void Prefix() => CampaignLoadPhaseDiagnosticPatch.RecordStarted("Campaign.OnSessionStart");

    [HarmonyPostfix]
    private static void Postfix() => CampaignLoadPhaseDiagnosticPatch.RecordCompleted("Campaign.OnSessionStart");
}

[HarmonyPatch(typeof(Hero), nameof(Hero.CheckInvalidEquipmentsAndReplaceIfNeeded))]
internal static class CheckInvalidHeroEquipmentDiagnosticPatch
{
    [HarmonyPrefix]
    private static void Prefix(Hero __instance) =>
        CampaignLoadPhaseDiagnosticPatch.RecordStarted($"Hero.CheckInvalidEquipmentsAndReplaceIfNeeded|{__instance.StringId}");

    [HarmonyPostfix]
    private static void Postfix(Hero __instance) =>
        CampaignLoadPhaseDiagnosticPatch.RecordCompleted($"Hero.CheckInvalidEquipmentsAndReplaceIfNeeded|{__instance.StringId}");
}

[HarmonyPatch(typeof(MBGameManager), nameof(MBGameManager.OnAfterGameInitializationFinished))]
internal static class AfterGameInitializationFinishedDiagnosticPatch
{
    [HarmonyPrefix]
    private static void Prefix() => CampaignLoadPhaseDiagnosticPatch.RecordStarted("MBGameManager.OnAfterGameInitializationFinished");

    [HarmonyPostfix]
    private static void Postfix() => CampaignLoadPhaseDiagnosticPatch.RecordCompleted("MBGameManager.OnAfterGameInitializationFinished");
}

[HarmonyPatch(typeof(SandBoxGameManager), nameof(SandBoxGameManager.OnLoadFinished))]
internal static class SandBoxGameManagerOnLoadFinishedDiagnosticPatch
{
    [HarmonyPrefix]
    private static void Prefix() => CampaignLoadPhaseDiagnosticPatch.RecordStarted("SandBoxGameManager.OnLoadFinished");

    [HarmonyPostfix]
    private static void Postfix() => CampaignLoadPhaseDiagnosticPatch.RecordCompleted("SandBoxGameManager.OnLoadFinished");
}

[HarmonyPatch]
internal static class DedicatedServerHostTickDiagnosticPatch
{
    [HarmonyPrepare]
    private static bool Prepare() => CampaignLoadPhaseDiagnosticPatch.GetDedicatedServerHostType() != null;

    [HarmonyTargetMethod]
    private static MethodBase TargetMethod() => AccessTools.Method(
        CampaignLoadPhaseDiagnosticPatch.GetDedicatedServerHostType(),
        "Tick",
        new[] { typeof(float) });

    [HarmonyPrefix]
    private static void Prefix() => CampaignLoadPhaseDiagnosticPatch.RecordStarted(
        $"CoopServerHost.Tick|{CampaignLoadPhaseDiagnosticPatch.GetDedicatedServerHostState()}");

    [HarmonyPostfix]
    private static void Postfix() => CampaignLoadPhaseDiagnosticPatch.RecordCompleted(
        $"CoopServerHost.Tick|{CampaignLoadPhaseDiagnosticPatch.GetDedicatedServerHostState()}");
}

[HarmonyPatch]
internal static class DedicatedServerHostOnLoadedDiagnosticPatch
{
    [HarmonyPrepare]
    private static bool Prepare() => CampaignLoadPhaseDiagnosticPatch.GetDedicatedServerHostType() != null;

    [HarmonyTargetMethod]
    private static MethodBase TargetMethod() => AccessTools.Method(
        CampaignLoadPhaseDiagnosticPatch.GetDedicatedServerHostType(),
        "OnLoaded");

    [HarmonyPrefix]
    private static void Prefix() => CampaignLoadPhaseDiagnosticPatch.RecordStarted(
        $"CoopServerHost.OnLoaded|{CampaignLoadPhaseDiagnosticPatch.GetDedicatedServerHostState()}");

    [HarmonyPostfix]
    private static void Postfix() => CampaignLoadPhaseDiagnosticPatch.RecordCompleted(
        $"CoopServerHost.OnLoaded|{CampaignLoadPhaseDiagnosticPatch.GetDedicatedServerHostState()}");
}

[HarmonyPatch(typeof(GameThread), nameof(GameThread.Update))]
internal static class GameThreadUpdateDiagnosticPatch
{
    private static int WatcherStarted;

    [HarmonyPrefix]
    private static void Prefix()
    {
        if (CampaignLoadPhaseDiagnosticPatch.GetDedicatedServerHostType() == null) return;

        CampaignLoadPhaseDiagnosticPatch.RecordStarted(
            $"GameThread.Update|{CampaignLoadPhaseDiagnosticPatch.GetDedicatedServerHostState()}");

        int generation = GameThread.BeginUpdateDiagnostic();
        if (CampaignLoadPhaseDiagnosticPatch.IsDedicatedServerServing() &&
            Interlocked.CompareExchange(ref WatcherStarted, 1, 0) == 0)
        {
            ThreadPool.QueueUserWorkItem(_ => ObserveStalledUpdate(generation));
        }
    }

    [HarmonyPostfix]
    private static void Postfix()
    {
        if (CampaignLoadPhaseDiagnosticPatch.GetDedicatedServerHostType() == null) return;

        GameThread.SetUpdateDiagnosticStage("harmony-postfix-entered");
        CampaignLoadPhaseDiagnosticPatch.RecordCompleted(
            $"GameThread.Update|{CampaignLoadPhaseDiagnosticPatch.GetDedicatedServerHostState()}");
        GameThread.SetUpdateDiagnosticStage("harmony-postfix-completed");
    }

    private static void ObserveStalledUpdate(int generation)
    {
        while (true)
        {
            Thread.Sleep(TimeSpan.FromSeconds(2));
            int currentGeneration = GameThread.UpdateDiagnosticGeneration;
            string stage = GameThread.UpdateDiagnosticStage;
            if (currentGeneration != generation)
            {
                generation = currentGeneration;
                continue;
            }
            if (stage == "harmony-postfix-completed") continue;

            CampaignLoadPhaseDiagnosticPatch.RecordStarted(
                $"GameThread.Update.Stalled|generation={generation}|stage={stage}|" +
                CampaignLoadPhaseDiagnosticPatch.GetDedicatedServerHostState(includeQueue: false));
            return;
        }
    }
}

[HarmonyPatch(typeof(CampaignTickCacheDataStore), nameof(CampaignTickCacheDataStore.InitializeDataCache))]
internal static class InitializeDataCacheDiagnosticPatch
{
    [HarmonyPrefix]
    private static void Prefix() => CampaignLoadPhaseDiagnosticPatch.RecordStarted("CampaignTickCacheDataStore.InitializeDataCache");

    [HarmonyPostfix]
    private static void Postfix() => CampaignLoadPhaseDiagnosticPatch.RecordCompleted("CampaignTickCacheDataStore.InitializeDataCache");
}
#endif
