using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Common.Util;
using E2E.Tests.Environment.Instance;
using GameInterface.Configuration;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages.Conversation;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// A player whose battle ended is in the post battle screens until their client finishes the encounter. The
/// occupancy pause waits for them, and with the AI amnesty on, AI parties can't engage them there or for the
/// amnesty seconds after. The battle ends through the server's real finalize and the player leaves the screens
/// through a real client <see cref="PlayerEncounter.Finish"/>, so every signal runs end to end.
/// </summary>
public class PostBattleScreensTests : MapEventTestBase, IDisposable
{
    private CampaignTimeControlMode? previousTimeMode;
    private readonly Dictionary<(EnvironmentInstance, string), MapEvent> clientBattles =
        new Dictionary<(EnvironmentInstance, string), MapEvent>();

    public PostBattleScreensTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void Amnesty_AiPartyCannotEngagePlayerStillInPostBattleScreens() => WithAmnesty(1, () =>
    {
        var (battleId, playerPartyId, aiPartyId) = SetupPlayerWithNearbyAiParty();
        Assert.True(CanEngage(aiPartyId, playerPartyId));

        DestroyServerMapEvent(battleId);
        // The screens aren't limited by the amnesty length.
        AdvanceServerClock(seconds: 60);

        Assert.False(CanEngage(aiPartyId, playerPartyId));
    });

    [Fact]
    public void Amnesty_RunsOutAfterLeavingScreens() => WithAmnesty(30, () =>
    {
        var (battleId, playerPartyId, aiPartyId) = SetupPlayerWithNearbyAiParty();
        DestroyServerMapEvent(battleId);
        LeavePostBattleScreens(Clients.First(), playerPartyId, battleId);

        AdvanceServerClock(seconds: 29);
        Assert.False(CanEngage(aiPartyId, playerPartyId));

        AdvanceServerClock(seconds: 2);
        Assert.True(CanEngage(aiPartyId, playerPartyId));
    });

    /// <summary>The other player's own encounter finish doesn't end the first player's screens.</summary>
    [Fact]
    public void Amnesty_OtherPlayerCanEngage_AndOnlyEndsItsOwnScreens() => WithAmnesty(1, () =>
    {
        var (battleId, playerPartyId, aiPartyId) = SetupPlayerWithNearbyAiParty();
        var (_, otherPlayerPartyId) = CreatePlayerHeroParty("2");
        TestEnvironment.ConnectRegisteredPlayer(Clients.Last(), "2");
        DestroyServerMapEvent(battleId);

        LeavePostBattleScreens(Clients.Last(), otherPlayerPartyId, battleId);
        AdvanceServerClock(seconds: 60);

        Assert.False(CanEngage(aiPartyId, playerPartyId));
        Assert.True(CanEngage(otherPlayerPartyId, playerPartyId));
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

        LeavePostBattleScreens(Clients.First(), playerPartyId, battleId);

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

        LeavePostBattleScreens(Clients.Last(), secondPartyId, battleId);

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

    /// <summary>Joining a running battle through the encounter menu finishes an encounter for that same battle.</summary>
    [Fact]
    public void FinishBeforeFinalize_DoesNotSkipTheScreens()
    {
        PlayServerTime();
        var (battleId, playerPartyId, _) = SetupPlayerWithNearbyAiParty();
        AcquireOccupancyPause();
        LeavePostBattleScreens(Clients.First(), playerPartyId, battleId);
        AssertServerTime(TimeControlEnum.Pause);

        DestroyServerMapEvent(battleId);

        AssertServerTime(TimeControlEnum.Pause);
    }

    /// <summary>A captured player finishes no encounter, so the release mustn't bring the screens back.</summary>
    [Fact]
    public void CapturedPlayer_IsNotInTheScreensWhenReleased()
    {
        PlayServerTime();
        var (heroId, partyId) = CreatePlayerHeroParty("1");
        TestEnvironment.ConnectRegisteredPlayer(Clients.First(), "1");
        var captorPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var battleId = CreateBattleStateDefeatMapEvent(heroId, partyId, captorPartyId);
        AcquireOccupancyPause();

        CommitBattleStateDefeat(battleId);
        var battleLeft = false;
        Server.Call(() => battleLeft = Server.ObjectManager.TryGetObject<MapEvent>(battleId, out _));
        if (battleLeft)
            DestroyServerMapEvent(battleId);
        ReleasePlayerAfterCaptorDefeated(heroId);
        PublishOnServer(new PlayerConnectionStateChanged());

        AssertServerTime(TimeControlEnum.Play_1x);
    }

    /// <summary>A failed or stale conversation approval sends the same message, but it isn't an encounter finish.</summary>
    [Fact]
    public void StaleApprovalDuringTheBattle_DoesNotSkipItsScreens()
    {
        PlayServerTime();
        var (battleId, _, _) = SetupPlayerWithNearbyAiParty();
        AcquireOccupancyPause();
        var client = Clients.First();
        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkConversationEnded("stale-request")));
        Server.PumpGameThread();

        DestroyServerMapEvent(battleId);

        AssertServerTime(TimeControlEnum.Pause);
    }

    /// <summary>Starting a new PvP battle finishes the old encounter (OpenHostileEncounter).</summary>
    [Fact]
    public void OldEncounterFinishDuringANewBattle_DoesNotSkipItsScreens()
    {
        PlayServerTime();
        var (battleId, playerPartyId, _) = SetupPlayerWithNearbyAiParty();
        AcquireOccupancyPause();
        LeavePostBattleScreens(Clients.First(), playerPartyId, finishedMapEventId: "MapEvent_Old");

        DestroyServerMapEvent(battleId);

        AssertServerTime(TimeControlEnum.Pause);
    }

    [Fact]
    public void ScreensEndAfterTheCap()
    {
        PlayServerTime();
        var (battleId, _, _) = SetupPlayerWithNearbyAiParty();
        AcquireOccupancyPause();
        var expiries = new List<Action>();
        Server.Call(() => Server.Resolve<PostBattleScreens>().Schedule = (_, expire) => expiries.Add(expire));
        DestroyServerMapEvent(battleId);
        AssertServerTime(TimeControlEnum.Pause);

        Server.Call(() => Assert.Single(expiries)());
        Server.PumpGameThread();

        AssertServerTime(TimeControlEnum.Play_1x);
    }

    /// <summary>Player "1" on the first client attacks an AI party; a second AI party stands next to the player.</summary>
    private (string battleId, string playerPartyId, string aiPartyId) SetupPlayerWithNearbyAiParty()
    {
        var ctx = CreateServerMapEvent();
        RegisterAsPlayerParty("1", TestEnvironment.CreateRegisteredObject<Hero>(), ctx.AttackerPartyId);
        TestEnvironment.ConnectRegisteredPlayer(Clients.First(), "1");
        foreach (var client in Clients)
            client.Call(() => clientBattles[(client, ctx.MapEventId)] = client.GetRegisteredObject<MapEvent>(ctx.MapEventId));

        return (ctx.MapEventId, ctx.AttackerPartyId, CreateServerParty());
    }

    // The encounter holds the client's own copy of the battle, kept from before the server destroyed it, so its id
    // comes the real way. An unknown id gets a stand in. FinalizeBattle is off so Finish doesn't try to end it.
    private void LeavePostBattleScreens(EnvironmentInstance client, string partyId, string finishedMapEventId)
    {
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Campaign.Current.MainParty = party;
        });
        var encounter = SetMockPlayerEncounter(client);
        client.Call(() =>
        {
            if (clientBattles.TryGetValue((client, finishedMapEventId), out var battle))
            {
                encounter._mapEvent = battle;
                return;
            }

            encounter._mapEvent = ObjectHelper.SkipConstructor<MapEvent>();
            using (new AllowedThread())
                encounter._mapEvent.StringId = finishedMapEventId;
        });
        EnableHeadlessEncounterFinish(client);

        client.Call(() => PlayerEncounter.Finish(), MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast)))
            .Append(AccessTools.Method(typeof(PlayerEncounter), nameof(PlayerEncounter.FinalizeBattle)))
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

    // Moves the server's post battle clock forward and holds it there.
    private void AdvanceServerClock(double seconds) => Server.Call(() =>
    {
        var screens = Server.Resolve<PostBattleScreens>();
        var now = screens.UtcNow().AddSeconds(seconds);
        screens.UtcNow = () => now;
    });

    // Set before any player connects, so the occupancy pause has a speed to go back to. It goes through TimePatches
    // because the TimeControlMode getter reads it from there, and Dispose puts the static mode back.
    private void PlayServerTime() => Server.Call(() =>
    {
        previousTimeMode ??= Campaign.Current.TimeControlMode;
        TimePatches.OverrideTimeControlMode(CampaignTimeControlMode.StoppablePlay);
    });

    public new void Dispose()
    {
        if (previousTimeMode is CampaignTimeControlMode mode)
            Server.Call(() => TimePatches.OverrideTimeControlMode(mode));
        base.Dispose();
    }

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
