#if COOP_NAVAL
using Common;
using Common.Util;
using Coop.Naval.CampaignBehaviors.Patches;
using GameInterface.Services.Players;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
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

    private static readonly List<string> SwitchedMenus = new();
    private static int raftStateChanges;
    private static MenuContext? currentMenuContext;

    private readonly bool wasServer = ModInformation.IsServer;
    private readonly Campaign? previousCampaign = Campaign.Current;
    private readonly Harmony harmony = new("coop.tests.naval.raft_state");
    private readonly Hero aiHero = (Hero)RuntimeHelpers.GetUninitializedObject(typeof(Hero));
    private readonly Hero playerHero = (Hero)RuntimeHelpers.GetUninitializedObject(typeof(Hero));
    private readonly MobileParty mainParty = ObjectHelper.SkipConstructor<MobileParty>();
    private readonly RaftStateCampaignBehavior behavior = new();

    public RaftStateCampaignBehaviorPatchesTests()
    {
        PlayerObjects.Add(playerHero, new ControlledObjectInfo("raft-state-test", null));

        SwitchedMenus.Clear();
        raftStateChanges = 0;
        currentMenuContext = null;

        var campaign = ObjectHelper.SkipConstructor<Campaign>();
        campaign.MainParty = mainParty;
        Campaign.Current = campaign;

        harmony.Patch(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.SwitchToMenu)),
            prefix: new HarmonyMethod(typeof(RaftStateCampaignBehaviorPatchesTests), nameof(SwitchToMenuStub)));
        harmony.Patch(AccessTools.Method(typeof(RaftStateChangeAction), nameof(RaftStateChangeAction.ApplyInternal)),
            prefix: new HarmonyMethod(typeof(RaftStateCampaignBehaviorPatchesTests), nameof(ApplyInternalStub)));
        harmony.Patch(AccessTools.PropertyGetter(typeof(Campaign), nameof(Campaign.CurrentMenuContext)),
            prefix: new HarmonyMethod(typeof(RaftStateCampaignBehaviorPatchesTests), nameof(CurrentMenuContextStub)));
        harmony.CreateClassProcessor(typeof(RaftStateCampaignBehaviorPatches)).Patch();
    }

    public void Dispose()
    {
        harmony.UnpatchAll(harmony.Id);
        Campaign.Current = previousCampaign;
        PlayerObjects.Remove(playerHero);
        ModInformation.IsServer = wasServer;
    }

    private static bool SwitchToMenuStub(string menuId)
    {
        SwitchedMenus.Add(menuId);
        return false;
    }

    private static bool ApplyInternalStub()
    {
        raftStateChanges++;
        return false;
    }

    private static bool CurrentMenuContextStub(ref MenuContext? __result)
    {
        __result = currentMenuContext;
        return false;
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

    // Continue after the server landed the party used to put only the client back on a raft
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RaftMenuContinue_Client_OnlyMovesToTheWaitMenu(bool inRaftState)
    {
        ModInformation.IsServer = false;
        mainParty._isInRaftState = inRaftState;

        behavior.player_raft_state_continue_on_consequence(null);

        Assert.Equal(inRaftState, mainParty.IsInRaftState);
        Assert.Equal(0, raftStateChanges);
        Assert.Equal("player_raft_state_wait", Assert.Single(SwitchedMenus));
    }

    // The wait menu's tick moves on to the washed ashore menu once the party is on land
    [Fact]
    public void RaftStateCleared_Client_MovesTheFirstRaftMenuToTheWaitMenu()
    {
        ModInformation.IsServer = false;
        mainParty._isInRaftState = false;
        currentMenuContext = CreateMenuContext("player_raft_state");

        behavior.OnMobilePartyRaftStateChanged(mainParty);

        Assert.Equal("player_raft_state_wait", Assert.Single(SwitchedMenus));
    }

    [Fact]
    public void RaftStateCleared_Client_LeavesOtherMenusAlone()
    {
        ModInformation.IsServer = false;
        mainParty._isInRaftState = false;
        currentMenuContext = CreateMenuContext("player_raft_state_wait");

        behavior.OnMobilePartyRaftStateChanged(mainParty);

        Assert.Empty(SwitchedMenus);
    }

    // Another party landing doesn't touch the main party's raft menu
    [Fact]
    public void RaftStateCleared_Client_IgnoresOtherParties()
    {
        ModInformation.IsServer = false;
        currentMenuContext = CreateMenuContext("player_raft_state");

        behavior.OnMobilePartyRaftStateChanged(ObjectHelper.SkipConstructor<MobileParty>());

        Assert.Empty(SwitchedMenus);
    }

    private static MenuContext CreateMenuContext(string menuId)
    {
        var menu = ObjectHelper.SkipConstructor<GameMenu>();
        menu.StringId = menuId;
        var menuContext = ObjectHelper.SkipConstructor<MenuContext>();
        menuContext.GameMenu = menu;
        return menuContext;
    }
}
#endif
