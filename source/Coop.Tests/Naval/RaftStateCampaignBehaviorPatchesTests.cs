#if COOP_NAVAL
using Common;
using Coop.Naval.CampaignBehaviors.Patches;
using GameInterface.Services.Players;
using HarmonyLib;
using System;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace Coop.Tests.Naval;

/// <summary>
/// Vanilla's raft behavior runs for AI heroes on the server, while players are stranded by PlayerRaftHandler instead.
/// </summary>
[Collection(ModInformationRoleCollection.Name)]
public class RaftStateCampaignBehaviorPatchesTests : IDisposable
{
    private static readonly ConditionalWeakTable<object, ControlledObjectInfo> PlayerObjects =
        (ConditionalWeakTable<object, ControlledObjectInfo>)AccessTools.Field(typeof(PlayerManager), "PlayerObjects").GetValue(null);

    private readonly bool wasServer = ModInformation.IsServer;
    private readonly Hero aiHero = (Hero)RuntimeHelpers.GetUninitializedObject(typeof(Hero));
    private readonly Hero playerHero = (Hero)RuntimeHelpers.GetUninitializedObject(typeof(Hero));

    public RaftStateCampaignBehaviorPatchesTests()
    {
        PlayerObjects.Add(playerHero, new ControlledObjectInfo("raft-state-test", null));
    }

    public void Dispose()
    {
        PlayerObjects.Remove(playerHero);
        ModInformation.IsServer = wasServer;
    }

    [Fact]
    public void OnHeroPrisonerReleased_Server_RunsOnlyForAnAiHero()
    {
        ModInformation.IsServer = true;

        Assert.True(RaftStateCampaignBehaviorPatches.OnHeroPrisonerReleasedPrefix(aiHero));
        Assert.False(RaftStateCampaignBehaviorPatches.OnHeroPrisonerReleasedPrefix(playerHero));
    }

    // The synced raft state opens the client's raft menu, so the client never runs it
    [Fact]
    public void OnHeroPrisonerReleased_Client_NeverRuns()
    {
        ModInformation.IsServer = false;

        Assert.False(RaftStateCampaignBehaviorPatches.OnHeroPrisonerReleasedPrefix(aiHero));
        Assert.False(RaftStateCampaignBehaviorPatches.OnHeroPrisonerReleasedPrefix(playerHero));
    }

    // An AI hero taken prisoner is left to vanilla's fugitive handling, which skips the raft
    [Fact]
    public void OnHeroPrisonerTaken_AiHero_DoesNothing()
    {
        ModInformation.IsServer = true;

        Assert.False(RaftStateCampaignBehaviorPatches.OnHeroPrisonerTakenPrefix(aiHero));
    }
}
#endif
