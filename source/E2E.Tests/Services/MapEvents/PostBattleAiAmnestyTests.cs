﻿using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Configuration;
using GameInterface.Services.Heroes.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// AI parties can't start an engagement with a player who is still in the post battle screens or in the
/// amnesty after them. The battle ends through the server's real finalize, the player leaves the screens
/// through a real client <see cref="PlayerEncounter.Finish"/> and the clock runs through the server's real
/// <c>Campaign.TickMapTime</c>, so every signal runs end to end.
/// </summary>
public class PostBattleAiAmnestyTests : MapEventTestBase
{
    public PostBattleAiAmnestyTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void AiPartyCannotEngagePlayerStillInPostBattleScreens()
    {
        var (battleId, playerPartyId, aiPartyId) = SetupPlayerWithNearbyAiParty();
        Assert.True(CanEngage(aiPartyId, playerPartyId));

        DestroyServerMapEvent(battleId);
        // The screens aren't limited by the amnesty length.
        RunServerClock(seconds: 60f, CampaignTimeControlMode.UnstoppablePlay);

        Assert.False(CanEngage(aiPartyId, playerPartyId));
    }

    [Fact]
    public void AmnestyRunsOutAfterLeavingScreens_OnlyWhileUnpaused()
    {
        var (battleId, playerPartyId, aiPartyId) = SetupPlayerWithNearbyAiParty();
        DestroyServerMapEvent(battleId);
        LeavePostBattleScreens(Clients.First(), playerPartyId);

        // Fast forward still counts real seconds.
        RunServerClock(seconds: 29f, CampaignTimeControlMode.UnstoppableFastForward);
        Assert.False(CanEngage(aiPartyId, playerPartyId));

        RunServerClock(seconds: 600f, CampaignTimeControlMode.Stop);
        Assert.False(CanEngage(aiPartyId, playerPartyId));

        RunServerClock(seconds: 2f, CampaignTimeControlMode.UnstoppablePlay);
        Assert.True(CanEngage(aiPartyId, playerPartyId));
    }

    [Fact]
    public void ZeroAmnesty_EndsProtectionWhenPlayerLeavesScreens()
    {
        var previousOptions = ModConfigProvider.ModOptions;
        ModConfigProvider.ModOptions = new ModOptions(new ModOptionsData { PostBattleAiAmnestySeconds = 0 });
        try
        {
            var (battleId, playerPartyId, aiPartyId) = SetupPlayerWithNearbyAiParty();
            DestroyServerMapEvent(battleId);
            Assert.False(CanEngage(aiPartyId, playerPartyId));

            LeavePostBattleScreens(Clients.First(), playerPartyId);

            Assert.True(CanEngage(aiPartyId, playerPartyId));
        }
        finally
        {
            ModConfigProvider.ModOptions = previousOptions;
        }
    }

    /// <summary>The other player's own encounter finish doesn't end the first player's screens.</summary>
    [Fact]
    public void OtherPlayer_CanStillEngage_AndItsNoticeLeavesTheFirstPlayerInTheScreens()
    {
        var (battleId, playerPartyId, aiPartyId) = SetupPlayerWithNearbyAiParty();
        var (_, otherPlayerPartyId) = CreatePlayerHeroParty("2");
        TestEnvironment.ConnectRegisteredPlayer(Clients.Last(), "2");
        DestroyServerMapEvent(battleId);

        LeavePostBattleScreens(Clients.Last(), otherPlayerPartyId);
        RunServerClock(seconds: 60f, CampaignTimeControlMode.UnstoppablePlay);

        Assert.False(CanEngage(aiPartyId, playerPartyId));
        Assert.True(CanEngage(otherPlayerPartyId, playerPartyId));
    }

    [Fact]
    public void AiPartyStillReachesASettlementWhileAPlayerIsProtected()
    {
        var (battleId, _, aiPartyId) = SetupPlayerWithNearbyAiParty();
        DestroyServerMapEvent(battleId);
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        var reached = false;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(aiPartyId, out var ai));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            EnsureEncounterModel();
            using (new AllowedThread())
            {
                ai.Position = settlement.GatePosition;
                ai.SetShortTermBehavior(AiBehavior.GoToSettlement, settlement.Party);
            }

            reached = ((IInteractablePoint)settlement.Party).CanPartyInteract(ai, 0f);
        });

        Assert.True(reached);
    }

    /// <summary>Player "1" on the first client attacks an AI party; a second AI party stands next to the player.</summary>
    private (string battleId, string playerPartyId, string aiPartyId) SetupPlayerWithNearbyAiParty()
    {
        var ctx = CreateServerMapEvent();
        RegisterAsPlayerParty("1", TestEnvironment.CreateRegisteredObject<Hero>(), ctx.AttackerPartyId);
        TestEnvironment.ConnectRegisteredPlayer(Clients.First(), "1");

        return (ctx.MapEventId, ctx.AttackerPartyId, CreateServerParty());
    }

    private void LeavePostBattleScreens(EnvironmentInstance client, string partyId)
    {
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Campaign.Current.MainParty = party;
        });
        SetMockPlayerEncounter(client);
        EnableHeadlessEncounterFinish(client);

        client.Call(() => PlayerEncounter.Finish(), MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast)))
            .ToList());

        AssertHasPlayerEncounter(client, expected: false);
    }

    // TickMapTime reads MainParty and the speed multiplier, which the constructor skipped harness campaign leaves unset.
    // The mode goes through TimePatches like the server sets it, because the TimeControlMode getter reads it from there.
    private void RunServerClock(float seconds, CampaignTimeControlMode mode)
    {
        Server.Call(() =>
        {
            Campaign.Current.MainParty ??= GameObjectCreator.CreateInitializedObject<MobileParty>();
            Campaign.Current.SpeedUpMultiplier = 4f;
            TimePatches.OverrideTimeControlMode(mode);
            Campaign.Current.TickMapTime(seconds);
        });
    }

    private bool CanEngage(string attackerPartyId, string targetPartyId)
    {
        var canEngage = false;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(attackerPartyId, out var attacker));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(targetPartyId, out var target));
            EnsureEncounterModel();
            // The attacker is heading for the target, the way the map AI sets it before an interaction.
            using (new AllowedThread())
            {
                attacker.ShortTermBehavior = AiBehavior.EngageParty;
                attacker.Ai.AiBehaviorInteractable = target.Party;
            }
            canEngage = ((IInteractablePoint)target.Party).CanPartyInteract(attacker, 0f);
        });
        return canEngage;
    }

    // The harness campaign has no encounter model, and the interaction distance check reads it.
    private static void EnsureEncounterModel()
    {
        if (Campaign.Current.Models.EncounterModel != null) return;

        var models = Campaign.Current.Models.GetGameModels().ToList();
        models.Add(new DefaultEncounterModel());
        Campaign.Current._gameModels = new GameModels(models);
    }
}
