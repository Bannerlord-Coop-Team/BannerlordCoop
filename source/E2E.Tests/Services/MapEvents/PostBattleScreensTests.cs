using Common.Messaging;
using Common.Network.Messages;
using Common.Util;
using E2E.Tests.Environment.Instance;
using GameInterface.Configuration;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Heroes.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// A player whose battle ended is in the post battle screens until their client finishes the encounter. The
/// occupancy pause waits for them, and with the AI amnesty on, AI parties can't engage them there or for the
/// amnesty seconds after. The battle ends through the server's real finalize and the player leaves the screens
/// through a real client <see cref="PlayerEncounter.Finish"/>, so every signal runs end to end.
/// </summary>
public class PostBattleScreensTests : MapEventTestBase
{
    public PostBattleScreensTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void AmnestyIsOffByDefault()
    {
        var (battleId, playerPartyId, aiPartyId) = SetupPlayerWithNearbyAiParty();

        DestroyServerMapEvent(battleId);

        Assert.True(CanEngage(aiPartyId, playerPartyId));
    }

    [Fact]
    public void Amnesty_AiPartyCannotEngagePlayerStillInPostBattleScreens() => WithAmnesty(1, () =>
    {
        var (battleId, playerPartyId, aiPartyId) = SetupPlayerWithNearbyAiParty();
        Assert.True(CanEngage(aiPartyId, playerPartyId));

        DestroyServerMapEvent(battleId);
        // The screens aren't limited by the amnesty length.
        Thread.Sleep(1100);

        Assert.False(CanEngage(aiPartyId, playerPartyId));
    });

    [Fact]
    public void Amnesty_RunsOutInRealSecondsAfterLeavingScreens() => WithAmnesty(1, () =>
    {
        var (battleId, playerPartyId, aiPartyId) = SetupPlayerWithNearbyAiParty();
        DestroyServerMapEvent(battleId);
        LeavePostBattleScreens(Clients.First(), playerPartyId);
        Assert.False(CanEngage(aiPartyId, playerPartyId));

        Thread.Sleep(1100);

        Assert.True(CanEngage(aiPartyId, playerPartyId));
    });

    /// <summary>The other player's own encounter finish doesn't end the first player's screens.</summary>
    [Fact]
    public void Amnesty_OtherPlayerCanStillEngage_AndItsNoticeLeavesTheFirstPlayerInTheScreens() => WithAmnesty(1, () =>
    {
        var (battleId, playerPartyId, aiPartyId) = SetupPlayerWithNearbyAiParty();
        var (_, otherPlayerPartyId) = CreatePlayerHeroParty("2");
        TestEnvironment.ConnectRegisteredPlayer(Clients.Last(), "2");
        DestroyServerMapEvent(battleId);

        LeavePostBattleScreens(Clients.Last(), otherPlayerPartyId);
        Thread.Sleep(1100);

        Assert.False(CanEngage(aiPartyId, playerPartyId));
        Assert.True(CanEngage(otherPlayerPartyId, playerPartyId));
    });

    [Fact]
    public void Amnesty_AiPartyStillReachesASettlement() => WithAmnesty(30, () =>
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
    });

    [Fact]
    public void BattleEnd_KeepsTimePausedWhileThePlayerIsInPostBattleScreens()
    {
        PlayServerTime();
        var (battleId, _, _) = SetupPlayerWithNearbyAiParty();
        AcquireOccupancyPause();

        DestroyServerMapEvent(battleId);

        AssertServerTime(TimeControlEnum.Pause);
    }

    [Fact]
    public void LeavingPostBattleScreens_ResumesTime()
    {
        PlayServerTime();
        var (battleId, playerPartyId, _) = SetupPlayerWithNearbyAiParty();
        AcquireOccupancyPause();
        DestroyServerMapEvent(battleId);
        AssertServerTime(TimeControlEnum.Pause);

        LeavePostBattleScreens(Clients.First(), playerPartyId);

        AssertServerTime(TimeControlEnum.Play_1x);
    }

    [Fact]
    public void TwoPlayersInTheBattle_TheFirstToLeaveResumesTime()
    {
        PlayServerTime();
        var (battleId, _, _) = SetupPlayerWithNearbyAiParty();
        var (_, secondPartyId) = CreatePlayerHeroParty("2");
        TestEnvironment.ConnectRegisteredPlayer(Clients.Last(), "2");
        JoinServerPartyToSide(battleId, secondPartyId, BattleSideEnum.Attacker);
        AcquireOccupancyPause();
        DestroyServerMapEvent(battleId);
        AssertServerTime(TimeControlEnum.Pause);

        LeavePostBattleScreens(Clients.Last(), secondPartyId);

        AssertServerTime(TimeControlEnum.Play_1x);
    }

    /// <summary>Player "2" in another battle keeps the pause, so only player "1"'s old screens could hold it.</summary>
    [Fact]
    public void DisconnectInPostBattleScreens_DropsThemSoTheReconnectedPlayerResumesTime()
    {
        PlayServerTime();
        var (battleId, playerPartyId, _) = SetupPlayerWithNearbyAiParty();
        var otherBattle = CreateServerMapEvent();
        RegisterAsPlayerParty("2", TestEnvironment.CreateRegisteredObject<Hero>(), otherBattle.AttackerPartyId);
        TestEnvironment.ConnectRegisteredPlayer(Clients.Last(), "2");
        AcquireOccupancyPause();
        DestroyServerMapEvent(battleId);
        AssertServerTime(TimeControlEnum.Pause);

        PublishOnServer(new PlayerDisconnected(Clients.First().NetPeer, default));
        AssertServerTime(TimeControlEnum.Pause);

        // Back on the map after the rejoin, the way the join flow activates the parked party.
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var party));
            party.IsActive = true;
        });
        TestEnvironment.ConnectRegisteredPlayer(Clients.First(), "1");
        PublishOnServer(new PlayerConnectionStateChanged());

        AssertServerTime(TimeControlEnum.Play_1x);
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

    private static void WithAmnesty(int seconds, Action test)
    {
        var previousOptions = ModConfigProvider.ModOptions;
        ModConfigProvider.ModOptions = new ModOptions(new ModOptionsData { PostBattleAiAmnestySeconds = seconds });
        try
        {
            test();
        }
        finally
        {
            ModConfigProvider.ModOptions = previousOptions;
        }
    }

    // Set before any player connects, so the occupancy pause has a speed to go back to. It goes through TimePatches
    // because the TimeControlMode getter reads it from there.
    private void PlayServerTime() =>
        Server.Call(() => TimePatches.OverrideTimeControlMode(CampaignTimeControlMode.StoppablePlay));

    private void PublishOnServer<T>(T message) where T : IMessage
    {
        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(this, message));
        Server.PumpGameThread();
    }

    private void AcquireOccupancyPause()
    {
        PublishOnServer(new PlayerConnectionStateChanged());
        AssertServerTime(TimeControlEnum.Pause);
    }

    private void AssertServerTime(TimeControlEnum expected) =>
        Server.Call(() => Assert.Equal(expected, Server.Resolve<ITimeControlInterface>().GetTimeControl()));

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
