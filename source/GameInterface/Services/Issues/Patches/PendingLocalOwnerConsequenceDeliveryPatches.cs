using Common;
using Common.Logging;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Players;
using HarmonyLib;
using Serilog;
using System;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.RegisterEvents))]
internal class PendingLocalOwnerConsequenceDeliveryPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<PendingLocalOwnerConsequenceDeliveryPatches>();
    private static readonly ConditionalWeakTable<IssuesCampaignBehavior, object> listenerRegistered = new();

    [HarmonyPostfix]
    private static void RegisterEventsPostfix(IssuesCampaignBehavior __instance)
    {
        if (listenerRegistered.TryGetValue(__instance, out _)) return;
        listenerRegistered.Add(__instance, null);

        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);
    }

    private static void OnHourlyTick()
    {
        if (ModInformation.IsClient) return;
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager)) return;
        if (!ContainerProvider.TryResolve<IPendingLocalOwnerConsequenceRegistry>(out var pendingConsequenceRegistry)) return;

        pendingConsequenceRegistry.FlushReady(playerManager, DeliverPendingQuestFailConsequence);
    }

    private static void DeliverPendingQuestFailConsequence(string controllerId, long obligationId, string questTypeKey, byte proof)
    {
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !ContainerProvider.TryResolve<INetwork>(out var network)) return;
        if (!playerManager.TryGetPeer(controllerId, out var peer)) return;

        try
        {
            network.Send(peer, new NetworkApplyPendingQuestFailConsequence(obligationId, questTypeKey, proof));
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to send pending quest-fail consequence {ObligationId} to controller {ControllerId} - will retry next tick", obligationId, controllerId);
        }
    }
}
