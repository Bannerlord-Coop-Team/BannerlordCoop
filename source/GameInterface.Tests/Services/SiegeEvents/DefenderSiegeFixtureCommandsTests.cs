#if DEBUG
using Autofac;
using Common;
using Common.Util;
using Coop.Tests.Mocks;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobilePartyAIs.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases.Extensions;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.SiegeEvents.Commands;
using GameInterface.Tests.Bootstrap;
using HarmonyLib;
using LiteNetLib;
using Moq;
using Newtonsoft.Json.Linq;
using SandBox.View.Map.Visuals;
using SandBox.View.Map.Managers;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

[Collection(nameof(CampaignCurrentCollection))]
public sealed class DefenderSiegeFixtureCommandsTests : IDisposable
{
    private static readonly FieldInfo RosterFixture = AccessTools.Field(typeof(DefenderSiegeFixtureCommands), "rosterFixture");
    private static readonly FieldInfo RosterCaptiveBaselineFixture = AccessTools.Field(
        typeof(DefenderSiegeFixtureCommands), "rosterCaptiveBaselineFixture");
    private static readonly Dictionary<PartyBase, MobilePartyVisual> Visuals = new();
    private static MobilePartyVisualManager visualManager;
    private static readonly List<LogEntry> Logs = new();
    private static readonly Dictionary<Hero, bool> PrisonerOverrides = new();
    private readonly Harmony harmony = new("Coop.Tests.DefenderRosterFixture");
    private readonly Campaign previousCampaign;
    private readonly bool previousServer;
    private readonly ILifetimeScope previousContainer;
    private readonly IContainer container;
    private readonly Dictionary<FieldInfo, object> previousFixtures = new();
    private readonly Dictionary<string, Player> registrations = new();
    private readonly Dictionary<string, NetPeer> peers = new();
    private readonly Dictionary<NetPeer, Player> peerPlayers = new();
    private readonly global::GameInterface.Services.ObjectManager.ObjectManager objects = new(Mock.Of<ILogger>());
    private readonly Mock<IPlayerManager> players = new();
    private readonly Mock<ICampaignSynchronization> synchronization = new(MockBehavior.Strict);
    private readonly Mock<IMobilePartyBehaviorSnapshot> snapshots = new();
    private readonly Mock<IDefenderFixtureCaptivityActions> actions = new(MockBehavior.Strict);
    private readonly Captive[] captives;
    private readonly List<string> actionCalls = new();
    private MobileParty replacementParty;
    private int synchronizationResolutions;
    private bool synchronized = true;
    private bool connected = true;
    private bool failNextSnapshotApply;
    private Action<Hero> afterRelease;
    private Action<Hero> afterRecapture;

    public DefenderSiegeFixtureCommandsTests()
    {
        GameBootStrap.Initialize();
        previousCampaign = Campaign.Current;
        previousServer = ModInformation.IsServer;
        ContainerProvider.TryGetContainer(out previousContainer);
        foreach (string name in new[]
                 { "rosterFixture", "rosterCaptiveBaselineFixture", "pendingCapture", "activeFixture", "restoredFixture" })
        {
            var field = AccessTools.Field(typeof(DefenderSiegeFixtureCommands), name);
            previousFixtures.Add(field, field.GetValue(null));
            field.SetValue(null, null);
        }
        try
        {
            ModInformation.IsServer = true;
            visualManager = ObjectHelper.SkipConstructor<MobilePartyVisualManager>();
            harmony.Patch(AccessTools.PropertyGetter(typeof(MobilePartyVisualManager), nameof(MobilePartyVisualManager.Current)),
                postfix: new HarmonyMethod(typeof(DefenderSiegeFixtureCommandsTests), nameof(ReadVisualManager)));
            // Supply only the scene's visual lookup; all command guards read real game objects.
            harmony.Patch(AccessTools.Method(typeof(PartyBaseExtensions), nameof(PartyBaseExtensions.GetPartyVisual)),
                prefix: new HarmonyMethod(typeof(DefenderSiegeFixtureCommandsTests), nameof(ReadVisual)));
            harmony.Patch(AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.IsPrisoner)),
                prefix: new HarmonyMethod(typeof(DefenderSiegeFixtureCommandsTests), nameof(ReadHeroIsPrisoner)));
            captives = new[] { CreateCaptive("testclient"), CreateCaptive("testclient2") };
            players.SetupGet(manager => manager.Players).Returns(() => registrations.Values.ToArray());
            players.Setup(manager => manager.IsConnected(It.IsAny<Player>())).Returns(() => connected);
            players.Setup(manager => manager.TryGetPlayer(It.IsAny<string>(), out It.Ref<Player>.IsAny))
                .Returns((string id, out Player player) => registrations.TryGetValue(id, out player));
            players.Setup(manager => manager.TryGetPeer(It.IsAny<string>(), out It.Ref<NetPeer>.IsAny))
                .Returns((string id, out NetPeer peer) => peers.TryGetValue(id, out peer));
            players.Setup(manager => manager.TryGetPlayer(It.IsAny<NetPeer>(), out It.Ref<Player>.IsAny))
                .Returns((NetPeer peer, out Player player) => peerPlayers.TryGetValue(peer, out player));
            synchronization.Setup(service => service.HasCompletedCampaignSynchronization(It.IsAny<NetPeer>()))
                .Returns(() => synchronized);
            snapshots.Setup(service => service.TryCreate(It.IsAny<MobileParty>(), out It.Ref<PartyBehaviorUpdateData>.IsAny))
                .Returns((MobileParty party, out PartyBehaviorUpdateData data) =>
                {
                    data = new PartyBehaviorUpdateData { PartyPosition = party.Position };
                    return true;
                });
            snapshots.Setup(service => service.CanApply(It.IsAny<MobileParty>(), It.IsAny<PartyBehaviorUpdateData>())).Returns(true);
            snapshots.Setup(service => service.TryApply(It.IsAny<MobileParty>(), It.IsAny<PartyBehaviorUpdateData>(), out It.Ref<IInteractablePoint>.IsAny))
                .Returns((MobileParty party, PartyBehaviorUpdateData data, out IInteractablePoint point) =>
                {
                    point = null;
                    if (failNextSnapshotApply)
                    {
                        failNextSnapshotApply = false;
                        return false;
                    }

                    party._position = data.PartyPosition;
                    return true;
                });
            actions.Setup(service => service.Release(It.IsAny<Hero>())).Callback<Hero>(hero =>
            {
                MobileParty captor = hero.PartyBelongedToAsPrisoner?.MobileParty;
                actionCalls.Add("release:" + hero.StringId);
                SetReleased(captives.Single(player => player.Hero == hero));
                if (captor?.IsActive == true)
                    DefaultMobilePartyAIModelPatches.PreventAttacksUntil(
                        captor,
                        captives.Single(player => player.Hero == hero).Party,
                        CreateFutureAttackProtectionDeadline());
                afterRelease?.Invoke(hero);
            });
            actions.Setup(service => service.Recapture(It.IsAny<PartyBase>(), It.IsAny<Hero>()))
                .Callback<PartyBase, Hero>((captor, hero) =>
                {
                    var player = captives.Single(player => player.Hero == hero);
                    Assert.NotNull(captor?.MobileParty);
                    actionCalls.Add("recapture:" + hero.StringId);
                    SetCaptured(player, captor.MobileParty);
                    afterRecapture?.Invoke(hero);
                });
            var builder = new ContainerBuilder();
            builder.RegisterInstance(objects).As<IObjectManager>();
            builder.RegisterType<DefenderFixtureBehaviorIdentity>().As<IDefenderFixtureBehaviorIdentity>().InstancePerDependency();
            builder.RegisterInstance(players.Object).As<IPlayerManager>();
            builder.Register(_ =>
            {
                synchronizationResolutions++;
                return synchronization.Object;
            }).As<ICampaignSynchronization>();
            builder.RegisterInstance(snapshots.Object).As<IMobilePartyBehaviorSnapshot>();
            builder.RegisterInstance(actions.Object).As<IDefenderFixtureCaptivityActions>();
            container = builder.Build();
            ContainerProvider.SetContainer(container);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    [Theory]
    [InlineData(false, "player-removed")]
    [InlineData(true, "player-removed")]
    [InlineData(false, "player-replaced")]
    [InlineData(true, "player-replaced")]
    [InlineData(false, "hero-removed")]
    [InlineData(true, "hero-removed")]
    [InlineData(false, "hero-rebound")]
    [InlineData(true, "hero-rebound")]
    [InlineData(false, "party-removed")]
    [InlineData(true, "party-removed")]
    [InlineData(false, "party-rebound")]
    [InlineData(true, "party-rebound")]
    public void Restore_RegistryDrift_RefusesWithoutActionsAndRetainsCapture(bool normalize, string drift)
    {
        Capture();
        if (normalize) Normalize();
        object captured = RosterFixture.GetValue(null);
        Drift(drift);
        object[] originalStates = captives.Select(ReadState).ToArray();
        object replacementState = replacementParty == null ? null : ReadPartyState(replacementParty);
        actionCalls.Clear();
        snapshots.Invocations.Clear();

        AssertRefusal(Restore(), captured);
        Assert.Equal(originalStates, captives.Select(ReadState).ToArray());
        Assert.Equal(replacementState, replacementParty == null ? null : ReadPartyState(replacementParty));
        Assert.Empty(actionCalls);
        AssertNoSnapshotReplay();
        Assert.False((bool)Restore()["restoredPendingVerification"]);
    }

    [Theory]
    [InlineData("player-replaced")]
    [InlineData("hero-removed")]
    [InlineData("hero-rebound")]
    [InlineData("party-removed")]
    [InlineData("party-rebound")]
    public void Verify_RegistryDriftAfterRestore_RetainsPendingFixture(string drift)
    {
        Capture();
        Normalize();
        AssertSuccess(Restore());
        object captured = RosterFixture.GetValue(null);
        Drift(drift);
        actionCalls.Clear();
        snapshots.Invocations.Clear();

        JObject result = Verify();
        AssertRefusal(result, captured, pending: true);
        Assert.True((bool)result["restoredPendingVerification"]);
        Assert.Empty(actionCalls);
        AssertNoSnapshotReplay();
    }

    [Fact]
    public void Restore_IdentityChangesDuringFirstRecapture_StopsBeforeReplayAndSecondRecapture()
    {
        Capture();
        Normalize();
        object captured = RosterFixture.GetValue(null);
        actionCalls.Clear();
        snapshots.Invocations.Clear();
        afterRecapture = _ => Drift("party-rebound");

        AssertRefusal(Restore(), captured);
        Assert.Equal(new[] { "recapture:hero_testclient" }, actionCalls);
        AssertNoSnapshotReplay();
        Assert.False(captives[1].Hero.IsPrisoner);
    }

    [Fact]
    public void Normalize_ReleaseThrowsAfterRegistryDrift_RollbackRefusesStaleObjects()
    {
        Capture();
        object captured = RosterFixture.GetValue(null);
        afterRelease = hero =>
        {
            if (hero != captives[1].Hero) return;
            Drift("hero-rebound");
            throw new InvalidOperationException("release callback failed");
        };

        JObject result = Parse(DefenderSiegeFixtureCommands.NormalizeRosterFixture(new()));
        AssertRefusal(result, captured);
        Assert.Contains("rollback", ((string)result["reason"]).ToLowerInvariant());
        Assert.Equal(new[] { "release:hero_testclient", "release:hero_testclient2" }, actionCalls);
        AssertNoSnapshotReplay();
    }

    [Fact]
    public void Normalize_LastReleaseChangesIdentity_DoesNotReportSuccess()
    {
        Capture();
        object captured = RosterFixture.GetValue(null);
        afterRelease = hero =>
        {
            if (hero == captives[1].Hero) Drift("player-removed");
        };

        AssertRefusal(Parse(DefenderSiegeFixtureCommands.NormalizeRosterFixture(new())), captured);
        Assert.Equal(2, actionCalls.Count);
        Assert.All(actionCalls, call => Assert.StartsWith("release:", call));
        AssertNoSnapshotReplay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RestoreAndVerify_ChangedCampaign_RefuseWithoutActions(bool clear)
    {
        Capture();
        Normalize();
        AssertSuccess(Restore());
        object captured = RosterFixture.GetValue(null);
        Campaign.Current = clear ? null : ObjectHelper.SkipConstructor<Campaign>();
        actionCalls.Clear();
        snapshots.Invocations.Clear();

        AssertRefusal(Restore(), captured, pending: true);
        AssertRefusal(Verify(), captured, pending: true);
        Assert.Empty(actionCalls);
        AssertNoSnapshotReplay();
    }

    [Fact]
    public void Capture_NoCampaign_RefusesWithoutActions()
    {
        Campaign.Current = null;
        Assert.False((bool)CaptureResult()["success"]);
        Assert.Null(RosterFixture.GetValue(null));
        Assert.Empty(actionCalls);
    }

    [Fact]
    public void Capture_LoadingPeer_RefusesBeforeSnapshotOrActions()
    {
        synchronized = false;
        Assert.False((bool)CaptureResult()["success"]);
        Assert.Null(RosterFixture.GetValue(null));
        Assert.Empty(actionCalls);
        snapshots.Verify(service => service.TryCreate(It.IsAny<MobileParty>(), out It.Ref<PartyBehaviorUpdateData>.IsAny), Times.Never);
    }

    [Fact]
    public void Normalize_PeerStartsLoading_RefusesAndRetainsCapture()
    {
        Capture();
        object captured = RosterFixture.GetValue(null);
        synchronized = false;
        AssertRefusal(Parse(DefenderSiegeFixtureCommands.NormalizeRosterFixture(new())), captured);
        Assert.Empty(actionCalls);
        Assert.False((bool)Restore()["normalized"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CaptureOrNormalize_MobileCaptorAtSea_RefusesWithoutRelease(bool capturedFirst)
    {
        if (capturedFirst) Capture();
        captives[0].Captor._isCurrentlyAtSea = true;
        JObject result = capturedFirst
            ? Parse(DefenderSiegeFixtureCommands.NormalizeRosterFixture(new()))
            : CaptureResult();
        Assert.False((bool)result["success"]);
        Assert.Empty(actionCalls);
        Assert.Equal(capturedFirst, RosterFixture.GetValue(null) != null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Lifecycle_UnchangedIdentities_RestoresAndClearsOnlyAfterVerification(bool disconnect)
    {
        Capture();
        Normalize();
        object captured = RosterFixture.GetValue(null);
        if (disconnect)
        {
            connected = false;
            peers.Clear();
            peerPlayers.Clear();
            foreach (Captive player in captives)
            {
                player.Party.IsActive = false;
                player.Party._isVisible = false;
                Visuals.Remove(player.Party.Party);
            }
        }
        int resolutionsBeforeRestore = synchronizationResolutions;
        synchronization.Invocations.Clear();
        AssertSuccess(Restore());
        Assert.Same(captured, RosterFixture.GetValue(null));
        Assert.All(captives, player =>
        {
            Assert.True(player.Hero.IsPrisoner);
            Assert.Same(player.Captor.Party, player.Hero.PartyBelongedToAsPrisoner);
            Assert.False(player.Party.IsActive);
            Assert.False(player.Party.IsVisible);
        });
        AssertSuccess(Verify());
        Assert.Null(RosterFixture.GetValue(null));
        Assert.Equal(new[] { "release:hero_testclient", "release:hero_testclient2",
            "recapture:hero_testclient", "recapture:hero_testclient2" }, actionCalls);
        synchronization.Verify(service => service.HasCompletedCampaignSynchronization(It.IsAny<NetPeer>()), Times.Never);
        Assert.Equal(resolutionsBeforeRestore, synchronizationResolutions);
    }

    [Fact]
    public void Restore_ConnectedPartyWithParkedShape_StillRefuses()
    {
        Capture();
        Normalize();
        object captured = RosterFixture.GetValue(null);
        captives[0].Party.IsActive = false;
        captives[0].Party._isVisible = false;
        Visuals.Remove(captives[0].Party.Party);
        actionCalls.Clear();

        AssertRefusal(Restore(), captured);
        Assert.Empty(actionCalls);
        AssertNoSnapshotReplay();
    }

    [Fact]
    public void PreparedCaptiveBaseline_CapturesAndRestoresThePreSetupPlayerState()
    {
        PrepareReadinessObservation();
        Captive target = captives[0];
        var preSetupCaptivityStart = target.Hero.CaptivityStartTime;
        var preSetupPosition = target.Party.Position;
        var preSetupBearing = target.Party.Bearing;

        AssertSuccess(PrepareCaptiveBaseline());
        Assert.NotNull(RosterCaptiveBaselineFixture.GetValue(null));
        Assert.True(captives[0].Hero.IsPrisoner);
        Assert.False(captives[1].Hero.IsPrisoner);
        Assert.Equal(1, captives[1].Party.PrisonRoster.GetTroopCount(target.Hero.CharacterObject));
        Assert.Equal(1, captives[1].Party.PrisonRoster.TotalManCount);

        Capture();
        Normalize();
        Assert.Equal(0, captives[1].Party.PrisonRoster.TotalManCount);
        AssertReleaseAttackProtectionPresent(captives[1].Party, target.Party);
        DefaultMobilePartyAIModelPatches.PreventAttacksUntil(
            captives[1].Party,
            captives[0].Captor,
            CreateFutureAttackProtectionDeadline());
        object captured = RosterFixture.GetValue(null);
        AssertRefusal(Restore(), captured);
        AssertReleaseAttackProtectionPresent(captives[1].Party, target.Party);
        DefaultMobilePartyAIModelPatches.RemoveAttackProtectionsForParty(captives[0].Captor);

        AssertSuccess(Restore());
        AssertNoAttackProtection(captives[1].Party, target.Party);
        AssertSuccess(Restore());
        AssertSuccess(Verify());
        Assert.Equal(1, captives[1].Party.PrisonRoster.GetTroopCount(target.Hero.CharacterObject));
        Assert.Equal(1, captives[1].Party.PrisonRoster.TotalManCount);
        JObject restored = RestoreCaptiveBaseline();
        AssertSuccess(restored);
        Assert.False(target.Hero.IsPrisoner);
        Assert.Same(target.Party, target.Hero.PartyBelongedTo);
        Assert.Null(target.Hero.PartyBelongedToAsPrisoner);
        Assert.Equal(Hero.CharacterStates.Active, target.Hero.HeroState);
        Assert.Equal(preSetupCaptivityStart, target.Hero.CaptivityStartTime);
        Assert.True(target.Party.IsActive);
        Assert.True(target.Party.IsVisible);
        Assert.Equal(preSetupPosition, target.Party.Position);
        Assert.Equal(preSetupBearing, target.Party.Bearing);
        Assert.Same(target.Hero, target.Party.LeaderHero);
        Assert.Equal(1, target.Party.MemberRoster.GetTroopCount(target.Hero.CharacterObject));
        Assert.Equal(1, target.Party.MemberRoster.TotalManCount);
        Assert.Equal(0, target.Party.PrisonRoster.TotalManCount);
        Assert.Equal(0, captives[1].Party.PrisonRoster.TotalManCount);
        Assert.True((bool)restored["restoredPendingVerification"]);
        AssertSuccess(VerifyCaptiveBaseline());
        Assert.Null(RosterCaptiveBaselineFixture.GetValue(null));
        Assert.Equal(new[]
        {
            "recapture:hero_testclient",
            "release:hero_testclient",
            "recapture:hero_testclient",
            "release:hero_testclient"
        }, actionCalls);
    }

    [Fact]
    public void PreparedCaptiveBaseline_RestoreRetriesAfterRecaptureSnapshotFailure()
    {
        PrepareReadinessObservation();
        Captive target = captives[0];
        AssertSuccess(PrepareCaptiveBaseline());
        Capture();
        Normalize();
        failNextSnapshotApply = true;
        object captured = RosterFixture.GetValue(null);

        AssertRefusal(Restore(), captured);
        Assert.True(target.Hero.IsPrisoner);
        Assert.Equal(1, captives[1].Party.PrisonRoster.GetTroopCount(target.Hero.CharacterObject));
        Assert.Equal(1, captives[1].Party.PrisonRoster.TotalManCount);
        AssertReleaseAttackProtectionPresent(captives[1].Party, target.Party);

        AssertSuccess(Restore());
        AssertNoAttackProtection(captives[1].Party, target.Party);
        AssertSuccess(Verify());
        AssertSuccess(RestoreCaptiveBaseline());
        AssertSuccess(VerifyCaptiveBaseline());
        Assert.Equal(new[]
        {
            "recapture:hero_testclient",
            "release:hero_testclient",
            "recapture:hero_testclient",
            "release:hero_testclient"
        }, actionCalls);
    }

    [Theory]
    [InlineData("target-additional-member")]
    [InlineData("captor-existing-prisoner")]
    public void PrepareCaptiveBaseline_RejectsAStateThatCannotBeRestored(string mutation)
    {
        PrepareReadinessObservation();
        if (mutation == "target-additional-member")
            captives[0].Party.MemberRoster.AddToCounts(captives[1].Hero.CharacterObject, 1);
        else
            captives[1].Party.PrisonRoster.AddToCounts(captives[0].Hero.CharacterObject, 1);

        Assert.False((bool)PrepareCaptiveBaseline()["success"]);
        Assert.Null(RosterCaptiveBaselineFixture.GetValue(null));
        Assert.Empty(actionCalls);
    }

    [Fact]
    public void PreparedCaptiveBaseline_RestoreRefusesUntilTheInnerRosterFixtureIsVerified()
    {
        PrepareReadinessObservation();
        AssertSuccess(PrepareCaptiveBaseline());
        object baseline = RosterCaptiveBaselineFixture.GetValue(null);
        Capture();
        actionCalls.Clear();

        JObject result = RestoreCaptiveBaseline();

        Assert.False((bool)result["success"]);
        Assert.Same(baseline, RosterCaptiveBaselineFixture.GetValue(null));
        Assert.Empty(actionCalls);
    }

    [Fact]
    public void PreparedCaptiveBaseline_MismatchedControllersRefuseWithoutReadingAStaleState()
    {
        PrepareReadinessObservation();
        AssertSuccess(PrepareCaptiveBaseline());

        JObject capture = Parse(DefenderSiegeFixtureCommands.CaptureRosterFixture(
            new() { "testclient", "another-client" }));
        JObject prepare = Parse(DefenderSiegeFixtureCommands.PrepareRosterCaptiveBaseline(
            new() { "testclient", "another-client" }));

        Assert.False((bool)capture["success"]);
        Assert.Contains("stale or belongs to other controllers", capture.Value<string>("reason"));
        Assert.False((bool)prepare["success"]);
        Assert.Contains("stale or belongs to other controllers", prepare.Value<string>("reason"));
        Assert.NotNull(RosterCaptiveBaselineFixture.GetValue(null));
        Assert.Equal(new[] { "recapture:hero_testclient" }, actionCalls);

        AssertSuccess(RestoreCaptiveBaseline());
        AssertSuccess(VerifyCaptiveBaseline());
    }

    [Fact]
    public void PreparedCaptiveBaseline_PartialCaptureFailureRetainsThePreSetupRestorePath()
    {
        PrepareReadinessObservation();
        afterRecapture = _ => throw new InvalidOperationException("capture callback failed");

        JObject prepared = PrepareCaptiveBaseline();

        Assert.False((bool)prepared["success"]);
        Assert.True((bool)prepared["restoreRequired"]);
        Assert.NotNull(RosterCaptiveBaselineFixture.GetValue(null));
        Assert.True(captives[0].Hero.IsPrisoner);
        Assert.Equal(1, captives[1].Party.PrisonRoster.GetTroopCount(captives[0].Hero.CharacterObject));
        Assert.Equal(1, captives[1].Party.PrisonRoster.TotalManCount);
        afterRecapture = null;

        AssertSuccess(RestoreCaptiveBaseline());
        AssertSuccess(VerifyCaptiveBaseline());
        Assert.False(captives[0].Hero.IsPrisoner);
        Assert.Same(captives[0].Party, captives[0].Hero.PartyBelongedTo);
        Assert.Equal(0, captives[1].Party.PrisonRoster.TotalManCount);
        Assert.Equal(new[] { "recapture:hero_testclient", "release:hero_testclient" }, actionCalls);
    }

    [Fact]
    public void CaptureRosterFixture_AllFreePlayersStillRefusesWithoutAPreparedBaseline()
    {
        PrepareReadinessObservation();

        JObject result = CaptureResult();

        Assert.False((bool)result["success"]);
        Assert.Contains("no captive", result.Value<string>("reason"));
        Assert.Null(RosterFixture.GetValue(null));
        Assert.Null(RosterCaptiveBaselineFixture.GetValue(null));
        Assert.Empty(actionCalls);
    }

    [Fact]
    public void CaptivityLogs_SuppressOnlyMatchingCallbackInsideFixtureAction()
    {
        InstallLogPatches();
        var behavior = new DefaultLogsCampaignBehavior();
        afterRelease = hero => AssertScopedLogs(behavior, hero, release: true);
        afterRecapture = hero => AssertScopedLogs(behavior, hero, release: false);
        Capture();
        Normalize();
        AssertSuccess(Restore());
        AssertSuccess(Verify());

        Logs.Clear();
        behavior.OnPrisonerTaken(captives[0].Captor.Party, captives[0].Hero);
        behavior.OnHeroPrisonerReleased(captives[0].Hero, captives[0].Captor.Party, null, default, true);
        Assert.IsType<TakePrisonerLogEntry>(Logs[0]);
        Assert.IsType<EndCaptivityLogEntry>(Logs[1]);
        Assert.Equal(2, Logs.Count);
    }

    [Fact]
    public void CaptivityLogs_ThrowingFixtureAction_RestoresLoggingScope()
    {
        InstallLogPatches();
        var behavior = new DefaultLogsCampaignBehavior();
        afterRelease = hero =>
        {
            behavior.OnHeroPrisonerReleased(hero, captives[0].Captor.Party, null, default, true);
            Assert.Empty(Logs);
            throw new InvalidOperationException("callback failed");
        };
        Capture();
        Assert.False((bool)Parse(DefenderSiegeFixtureCommands.NormalizeRosterFixture(new()))["success"]);

        behavior.OnHeroPrisonerReleased(captives[0].Hero, captives[0].Captor.Party, null, default, true);
        Assert.IsType<EndCaptivityLogEntry>(Assert.Single(Logs));
    }

    [Theory]
    [InlineData("stage", "campaign-null")]
    [InlineData("stage", "campaign-replaced")]
    [InlineData("stage", "settlement-removed")]
    [InlineData("stage", "settlement-rebound")]
    [InlineData("stage", "player-removed")]
    [InlineData("stage", "player-replaced")]
    [InlineData("stage", "party-removed")]
    [InlineData("stage", "party-rebound")]
    [InlineData("restore", "campaign-null")]
    [InlineData("restore", "campaign-replaced")]
    [InlineData("restore", "settlement-removed")]
    [InlineData("restore", "settlement-rebound")]
    [InlineData("restore", "player-removed")]
    [InlineData("restore", "player-replaced")]
    [InlineData("restore", "party-removed")]
    [InlineData("restore", "party-rebound")]
    [InlineData("verify", "campaign-null")]
    [InlineData("verify", "campaign-replaced")]
    [InlineData("verify", "settlement-removed")]
    [InlineData("verify", "settlement-rebound")]
    [InlineData("verify", "player-removed")]
    [InlineData("verify", "player-replaced")]
    [InlineData("verify", "party-removed")]
    [InlineData("verify", "party-rebound")]
    public void StagingFixture_IdentityDrift_RefusesWithoutReplayAndRetainsCapture(string phase, string drift)
    {
        Settlement settlement = CaptureStagingFixture();
        if (phase != "stage") AssertSuccess(Parse(DefenderSiegeFixtureCommands.Stage(new())));
        if (phase == "verify") AssertSuccess(Parse(DefenderSiegeFixtureCommands.Restore(new())));
        string fieldName = phase == "stage" ? "pendingCapture" : phase == "restore" ? "activeFixture" : "restoredFixture";
        var field = AccessTools.Field(typeof(DefenderSiegeFixtureCommands), fieldName);
        object captured = field.GetValue(null);
        switch (drift)
        {
            case "campaign-null": Campaign.Current = null; break;
            case "campaign-replaced": Campaign.Current = ObjectHelper.SkipConstructor<Campaign>(); break;
            case "settlement-removed": Assert.True(objects.Remove(settlement)); break;
            case "settlement-rebound":
                Assert.True(objects.Remove(settlement));
                Assert.True(objects.AddExisting("castle_ES1", ObjectHelper.SkipConstructor<Settlement>()));
                break;
            default: Drift(drift); break;
        }
        object[] originalStates = captives.Select(ReadState).ToArray();
        snapshots.Invocations.Clear();
        actionCalls.Clear();
        JObject result = Parse(phase == "stage" ? DefenderSiegeFixtureCommands.Stage(new()) :
            phase == "restore" ? DefenderSiegeFixtureCommands.Restore(new()) :
            DefenderSiegeFixtureCommands.VerifyRestore(new()));

        Assert.False((bool)result["success"], result.ToString());
        Assert.Same(captured, field.GetValue(null));
        Assert.Equal(originalStates, captives.Select(ReadState).ToArray());
        Assert.Empty(actionCalls);
        AssertNoSnapshotReplay();
    }

    [Fact]
    public void StagingFixture_UnchangedIdentities_ClearsOnlyAfterRestoreVerification()
    {
        CaptureStagingFixture();
        AssertSuccess(Parse(DefenderSiegeFixtureCommands.Stage(new())));
        AssertSuccess(Parse(DefenderSiegeFixtureCommands.Restore(new())));
        var field = AccessTools.Field(typeof(DefenderSiegeFixtureCommands), "restoredFixture");
        Assert.NotNull(field.GetValue(null));
        AssertSuccess(Parse(DefenderSiegeFixtureCommands.VerifyRestore(new())));
        Assert.Null(field.GetValue(null));
        snapshots.Verify(service => service.TryApply(It.IsAny<MobileParty>(),
            It.IsAny<PartyBehaviorUpdateData>(), out It.Ref<IInteractablePoint>.IsAny), Times.Exactly(2));
    }

    [Theory]
    [InlineData("anchor", "removed")]
    [InlineData("anchor", "rebound")]
    [InlineData("anchor", "anchor-replaced")]
    [InlineData("anchor", "unchanged")]
    [InlineData("interactable", "removed")]
    [InlineData("interactable", "rebound")]
    [InlineData("interactable", "unchanged")]
    [InlineData("target-party", "removed")]
    [InlineData("target-party", "rebound")]
    [InlineData("target-party", "unchanged")]
    [InlineData("target-settlement", "removed")]
    [InlineData("target-settlement", "rebound")]
    [InlineData("target-settlement", "unchanged")]
    [InlineData("move-target-party", "removed")]
    [InlineData("move-target-party", "rebound")]
    [InlineData("move-target-party", "unchanged")]
    public void StagingFixture_BehaviorReferenceDrift_PreservesIdentityBeforeMutation(string reference, string drift)
    {
        var target = ObjectHelper.SkipConstructor<MobileParty>();
        target.Party = ObjectHelper.SkipConstructor<PartyBase>();
        target.Party.MobileParty = target;
        target.Anchor = new AnchorPoint(target);
        string id = reference == "interactable" ? "PartyBase_behavior-target" :
            reference == "target-settlement" ? "Settlement_behavior-target" : "MobileParty_behavior-target";
        object capturedTarget = reference == "interactable" ? target.Party :
            reference == "target-settlement" ? ObjectHelper.SkipConstructor<Settlement>() : target;
        Assert.True(objects.AddExisting(id, capturedTarget));
        snapshots.Setup(service => service.TryCreate(It.IsAny<MobileParty>(), out It.Ref<PartyBehaviorUpdateData>.IsAny))
            .Returns((MobileParty party, out PartyBehaviorUpdateData data) =>
            {
                data = new PartyBehaviorUpdateData(
                    null, default, reference == "anchor" || reference == "interactable" ? "behavior-target" : null,
                    default, party.Position, default, default, default);
                if (reference == "anchor")
                {
                    party.Ai.AiBehaviorInteractable = target.Anchor;
                    data.IsInteractableAnchor = true;
                }
                else if (reference == "interactable")
                {
                    party.Ai.AiBehaviorInteractable = target.Party;
                }
                else if (reference == "target-party")
                {
                    party.TargetParty = target;
                    data.TargetPartyId = "behavior-target";
                }
                else if (reference == "target-settlement")
                {
                    party._targetSettlement = (Settlement)capturedTarget;
                    data.TargetSettlementId = "behavior-target";
                }
                else
                {
                    party.MoveTargetParty = target;
                    data.MoveTargetPartyId = "behavior-target";
                }
                return true;
            });
        CaptureStagingFixture();
        AssertSuccess(Parse(DefenderSiegeFixtureCommands.Stage(new())));
        if (drift == "unchanged")
        {
            AssertSuccess(Parse(DefenderSiegeFixtureCommands.Restore(new())));
            AssertSuccess(Parse(DefenderSiegeFixtureCommands.VerifyRestore(new())));
            return;
        }
        if (drift == "anchor-replaced") target.Anchor = new AnchorPoint(target);
        else Assert.True(objects.Remove(capturedTarget));
        if (drift == "rebound")
        {
            object replacement = reference == "interactable" ? ObjectHelper.SkipConstructor<PartyBase>() :
                reference == "target-settlement" ? ObjectHelper.SkipConstructor<Settlement>() :
                ObjectHelper.SkipConstructor<MobileParty>();
            Assert.True(objects.AddExisting(id, replacement));
        }
        var field = AccessTools.Field(typeof(DefenderSiegeFixtureCommands), "activeFixture");
        object capture = field.GetValue(null);
        // Make a failed late lookup observable even when the original and staged settlement agree.
        captives[0].Party.Bearing = new Vec2(0.5f, 0.75f);
        captives[0].Party.LastVisitedSettlement = null;
        object[] before = captives.Select(ReadState).ToArray();
        snapshots.Invocations.Clear();

        Assert.False((bool)Parse(DefenderSiegeFixtureCommands.Restore(new()))["success"]);
        Assert.Equal(before, captives.Select(ReadState).ToArray());
        Assert.Same(capture, field.GetValue(null));
        AssertNoSnapshotReplay();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void StagingFixture_AfterAssaultDisconnect_RestoresWorldStateAndKeepsOfflinePartyParked(int disconnectedCount)
    {
        Settlement settlement = CaptureStagingFixture();
        AssertSuccess(Parse(DefenderSiegeFixtureCommands.Stage(new())));
        using var context = new DefenderSiegeContextFixtureTests.AssaultFixture(
            settlement, captives.Select(value => value.Party).ToArray(), objects, registrations);
        players.Setup(manager => manager.IsConnected(It.IsAny<Player>()))
            .Returns((Player player) => !context.Disconnected.Contains(player.ControllerId));
        foreach (var captive in captives) Visuals.Remove(captive.Party.Party);
        foreach (string id in registrations.Keys.Take(disconnectedCount)) context.Disconnect(id);
        Assert.All(captives, captive => Assert.True(captive.Party.IsActive));

        Assert.True(context.Fixture.EndMissions().Succeeded);
        Assert.True(context.Fixture.Restore().Succeeded);
        Assert.True(context.Fixture.Verify().Succeeded);
        foreach (var captive in captives.Take(disconnectedCount))
        {
            Assert.False(captive.Party.IsActive);
            Assert.False(captive.Party.IsVisible);
            captive.Party.Bearing = new Vec2(0.5f, 0.75f);
        }
        AssertSuccess(Parse(DefenderSiegeFixtureCommands.Restore(new())));
        AssertSuccess(Parse(DefenderSiegeFixtureCommands.VerifyRestore(new())));
        Assert.Null(AccessTools.Field(typeof(DefenderSiegeFixtureCommands), "activeFixture").GetValue(null));
        foreach (var captive in captives.Take(disconnectedCount))
        {
            Assert.False(captive.Party.IsActive);
            Assert.False(captive.Party.IsVisible);
        }
        snapshots.Verify(service => service.TryApply(It.IsAny<MobileParty>(),
            It.IsAny<PartyBehaviorUpdateData>(), out It.Ref<IInteractablePoint>.IsAny), Times.Exactly(2));
    }

    [Fact]
    public void StagingFixture_InactiveConnectedParty_StillRefusesRestoration()
    {
        CaptureStagingFixture();
        AssertSuccess(Parse(DefenderSiegeFixtureCommands.Stage(new())));
        captives[0].Party.IsActive = false;
        captives[0].Party.IsVisible = false;
        snapshots.Invocations.Clear();

        Assert.False((bool)Parse(DefenderSiegeFixtureCommands.Restore(new()))["success"]);
        Assert.NotNull(AccessTools.Field(typeof(DefenderSiegeFixtureCommands), "activeFixture").GetValue(null));
        AssertNoSnapshotReplay();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void StagingFixture_DisconnectedPartyNotParked_RefusesRestoreAndVerify(bool active, bool visible)
    {
        CaptureStagingFixture();
        AssertSuccess(Parse(DefenderSiegeFixtureCommands.Stage(new())));
        players.Setup(manager => manager.IsConnected(It.IsAny<Player>()))
            .Returns((Player player) => !ReferenceEquals(player, captives[0].Player));
        captives[0].Party.IsActive = active;
        captives[0].Party.IsVisible = visible;
        object[] before = captives.Select(ReadState).ToArray();
        snapshots.Invocations.Clear();

        Assert.False((bool)Parse(DefenderSiegeFixtureCommands.Restore(new()))["success"]);
        Assert.False((bool)Parse(DefenderSiegeFixtureCommands.VerifyRestore(new()))["success"]);
        Assert.Equal(before, captives.Select(ReadState).ToArray());
        Assert.NotNull(AccessTools.Field(typeof(DefenderSiegeFixtureCommands), "activeFixture").GetValue(null));
        AssertNoSnapshotReplay();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void StagingFixture_DisconnectedPartyDriftsAfterRestore_RefusesVerification(bool active, bool visible)
    {
        CaptureStagingFixture();
        AssertSuccess(Parse(DefenderSiegeFixtureCommands.Stage(new())));
        AssertSuccess(Parse(DefenderSiegeFixtureCommands.Restore(new())));
        var field = AccessTools.Field(typeof(DefenderSiegeFixtureCommands), "restoredFixture");
        object capture = field.GetValue(null);
        Assert.NotNull(capture);
        players.Setup(manager => manager.IsConnected(It.IsAny<Player>()))
            .Returns((Player player) => !ReferenceEquals(player, captives[0].Player));
        captives[0].Party.IsActive = active;
        captives[0].Party.IsVisible = visible;
        snapshots.Invocations.Clear();

        Assert.False((bool)Parse(DefenderSiegeFixtureCommands.VerifyRestore(new()))["success"]);
        Assert.Same(capture, field.GetValue(null));
        AssertNoSnapshotReplay();
    }

    [Fact]
    public void StagingCapture_LoadingPeer_RefusesBeforeSnapshot()
    {
        PrepareStagingParties();
        synchronized = false;
        snapshots.Invocations.Clear();

        var result = Parse(DefenderSiegeFixtureCommands.Capture(new() { "testclient", "testclient2" }));

        Assert.False((bool)result["success"]);
        Assert.Contains("synchronization", (string)result["reason"]);
        Assert.Null(AccessTools.Field(typeof(DefenderSiegeFixtureCommands), "pendingCapture").GetValue(null));
        snapshots.Verify(service => service.TryCreate(It.IsAny<MobileParty>(), out It.Ref<PartyBehaviorUpdateData>.IsAny), Times.Never);
    }

    [Fact]
    public void Stage_PeerStartsLoading_RefusesAndRetainsPendingCapture()
    {
        CaptureStagingFixture();
        var field = AccessTools.Field(typeof(DefenderSiegeFixtureCommands), "pendingCapture");
        object captured = field.GetValue(null);
        synchronized = false;
        snapshots.Invocations.Clear();

        var result = Parse(DefenderSiegeFixtureCommands.Stage(new()));

        Assert.False((bool)result["success"]);
        Assert.Contains("synchronization", (string)result["reason"]);
        Assert.Same(captured, field.GetValue(null));
        Assert.Null(AccessTools.Field(typeof(DefenderSiegeFixtureCommands), "activeFixture").GetValue(null));
        AssertNoSnapshotReplay();
        synchronized = true;
        AssertSuccess(Parse(DefenderSiegeFixtureCommands.Stage(new())));
    }

    [Fact]
    public void ObserveRosterFixtureReadiness_ReleasedPlayersReportEveryGuardReady()
    {
        PrepareReadinessObservation();

        JObject result = ObserveReadiness();

        AssertSuccess(result);
        Assert.Equal("readiness", result.Value<string>("phase"));
        Assert.True(result.Value<bool>("observationComplete"));
        Assert.True(result.Value<bool>("capturePreconditionsCurrent"));
        JArray rows = Assert.IsType<JArray>(result["players"]);
        Assert.Equal(2, rows.Count);
        foreach (JObject row in rows.Values<JObject>())
        {
            Assert.True(row.Value<bool>("captureReady"));
            Assert.Empty(row["failedConditions"].Values<string>());
            Assert.True(row.Value<bool>("playerResolved"));
            Assert.True(row.Value<bool>("heroResolved"));
            Assert.True(row.Value<bool>("partyResolved"));
            Assert.True(row.Value<bool>("partyIdentityCurrent"));
            Assert.True(row.Value<bool>("partyBaseResolved"));
        }
        AssertReadinessObservationIsNonMutating();
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, false, true)]
    public void RosterReadinessAndCapture_UseTheServerVisualManagerRequirement(
        bool managerPresent, bool visualPresent, bool expectedReady)
    {
        if (!managerPresent) visualManager = null;
        PrepareReadinessObservation();
        if (!visualPresent) Visuals.Clear();

        JObject result = ObserveReadiness();
        foreach (JObject row in result["players"].Values<JObject>())
        {
            Assert.Equal(managerPresent, row.Value<bool>("visualManagerPresent"));
            Assert.Equal(visualPresent, row.Value<bool>("partyHasVisual"));
            Assert.Equal(expectedReady, row.Value<bool>("captureReady"));
            Assert.Equal(expectedReady ? Array.Empty<string>() : new[] { "partyHasVisual" },
                row["failedConditions"].Values<string>().ToArray());
        }
        AssertReadinessObservationIsNonMutating();
        SetCaptured(captives[1]);
        JObject capture = CaptureResult();
        Assert.Equal(expectedReady, capture.Value<bool>("success"));
        if (expectedReady)
        {
            AssertSuccess(Restore());
            AssertSuccess(Verify());
        }
    }

    [Fact]
    public void HeadlessRosterFixture_NormalizesAndRestoresCaptivesWithoutVisuals()
    {
        visualManager = null;
        Capture();
        Normalize();
        Assert.Empty(Visuals);
        AssertSuccess(Restore());
        AssertSuccess(Verify());
        Assert.Null(RosterFixture.GetValue(null));
    }

    [Theory]
    [InlineData("heroIsPrisoner", false)]
    [InlineData("heroHasCaptor", false)]
    [InlineData("heroBelongsToPlayerParty", true)]
    [InlineData("heroStateIsActive", true)]
    [InlineData("partyActive", true)]
    [InlineData("partyVisible", true)]
    [InlineData("partyLeaderIsHero", true)]
    [InlineData("partyHasMapEvent", false)]
    [InlineData("partyHasBesiegerCamp", false)]
    [InlineData("partyIsTransitioning", false)]
    [InlineData("partyHasArmy", false)]
    [InlineData("partyHasAttachedTo", false)]
    [InlineData("partyHasAttachedParties", false)]
    [InlineData("partyIsAtSea", false)]
    public void HeadlessRosterReadiness_PreservesEveryNonvisualGuard(string guardName, bool requiredValue)
    {
        visualManager = null;
        PrepareReadinessObservation();
        SetReadinessGuardFailure(guardName);
        JObject row = ReadinessRow(ObserveReadiness(), captives[0].Player.ControllerId);
        Assert.False(row.Value<bool>("captureReady"));
        Assert.Equal(!requiredValue, row.Value<bool>(guardName));
        Assert.Equal(new[] { guardName }, row["failedConditions"].Values<string>().ToArray());
        AssertReadinessObservationIsNonMutating();
        SetCaptured(captives[1]);
        Assert.False(CaptureResult().Value<bool>("success"));
        Assert.Null(RosterFixture.GetValue(null));
    }

    [Theory]
    [InlineData("heroIsPrisoner", false)]
    [InlineData("heroHasCaptor", false)]
    [InlineData("heroBelongsToPlayerParty", true)]
    [InlineData("heroStateIsActive", true)]
    [InlineData("partyActive", true)]
    [InlineData("partyVisible", true)]
    [InlineData("partyHasVisual", true)]
    [InlineData("partyLeaderIsHero", true)]
    [InlineData("partyHasMapEvent", false)]
    [InlineData("partyHasBesiegerCamp", false)]
    [InlineData("partyIsTransitioning", false)]
    [InlineData("partyHasArmy", false)]
    [InlineData("partyHasAttachedTo", false)]
    [InlineData("partyHasAttachedParties", false)]
    [InlineData("partyIsAtSea", false)]
    public void ObserveRosterFixtureReadiness_ReportsEachGuardFailureWithItsExactPolarity(
        string guardName,
        bool requiredValue)
    {
        PrepareReadinessObservation();
        SetReadinessGuardFailure(guardName);

        JObject result = ObserveReadiness();
        JObject row = ReadinessRow(result, captives[0].Player.ControllerId);

        AssertSuccess(result);
        Assert.True(result.Value<bool>("observationComplete"));
        Assert.False(row.Value<bool>("captureReady"));
        Assert.Equal(!requiredValue, row.Value<bool>(guardName));
        Assert.Equal(new[] { guardName }, row["failedConditions"].Values<string>().ToArray());
        AssertReadinessObservationIsNonMutating();
    }

    [Theory]
    [InlineData("missing-player", false, false, false, false, false)]
    [InlineData("missing-hero", true, false, true, true, true)]
    [InlineData("missing-party", true, true, false, false, false)]
    [InlineData("reverse-party-identity", true, true, true, false, true)]
    [InlineData("missing-party-base", true, true, true, true, false)]
    public void ObserveRosterFixtureReadiness_IncompleteIdentityIsNonReady(
        string missingIdentity,
        bool playerResolved,
        bool heroResolved,
        bool partyResolved,
        bool partyIdentityCurrent,
        bool partyBaseResolved)
    {
        PrepareReadinessObservation();
        IObjectManager observationObjects = objects;
        switch (missingIdentity)
        {
            case "missing-player":
                registrations.Remove(captives[0].Player.ControllerId);
                break;
            case "missing-hero":
                Assert.True(objects.Remove(captives[0].Hero));
                break;
            case "missing-party":
                Assert.True(objects.Remove(captives[0].Party));
                break;
            case "reverse-party-identity":
                observationObjects = CreateReversePartyIdentityObjectManager();
                break;
            case "missing-party-base":
                captives[0].Party.Party = null;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(missingIdentity));
        }

        JObject result = observationObjects == objects
            ? ObserveReadiness()
            : ObserveReadiness(observationObjects);
        JObject row = ReadinessRow(result, captives[0].Player.ControllerId);

        AssertSuccess(result);
        Assert.False(result.Value<bool>("observationComplete"));
        Assert.False(result.Value<bool>("capturePreconditionsCurrent"));
        Assert.Equal(playerResolved, row.Value<bool>("playerResolved"));
        Assert.Equal(heroResolved, row.Value<bool>("heroResolved"));
        Assert.Equal(partyResolved, row.Value<bool>("partyResolved"));
        Assert.Equal(partyIdentityCurrent, row.Value<bool>("partyIdentityCurrent"));
        Assert.Equal(partyBaseResolved, row.Value<bool>("partyBaseResolved"));
        Assert.Equal(JTokenType.Null, row["captureReady"].Type);
        AssertReadinessObservationIsNonMutating();
    }

    private void PrepareReadinessObservation()
    {
        if (Campaign.Current == null) Campaign.Current = ObjectHelper.SkipConstructor<Campaign>();
        foreach (Captive captive in captives) SetReleased(captive);
        PrisonerOverrides.Clear();
        actionCalls.Clear();
        snapshots.Invocations.Clear();
    }

    private void SetReadinessGuardFailure(string guardName)
    {
        Captive captive = captives[0];
        switch (guardName)
        {
            case "heroIsPrisoner":
                PrisonerOverrides[captive.Hero] = true;
                break;
            case "heroHasCaptor":
                captive.Hero.PartyBelongedToAsPrisoner = captive.Captor.Party;
                break;
            case "heroBelongsToPlayerParty":
                captive.Hero._partyBelongedTo = CreateParty("other-player-party");
                break;
            case "heroStateIsActive":
                captive.Hero._heroState = Hero.CharacterStates.Released;
                break;
            case "partyActive":
                captive.Party.IsActive = false;
                break;
            case "partyVisible":
                captive.Party._isVisible = false;
                break;
            case "partyHasVisual":
                Visuals.Remove(captive.Party.Party);
                break;
            case "partyLeaderIsHero":
                captive.Party._partyComponent = null;
                break;
            case "partyHasMapEvent":
                var side = ObjectHelper.SkipConstructor<MapEventSide>();
                AccessTools.Field(typeof(MapEventSide), "_mapEvent").SetValue(side, ObjectHelper.SkipConstructor<MapEvent>());
                captive.Party.Party._mapEventSide = side;
                break;
            case "partyHasBesiegerCamp":
                captive.Party._besiegerCamp = ObjectHelper.SkipConstructor<BesiegerCamp>();
                break;
            case "partyIsTransitioning":
                captive.Party.NavigationTransitionStartTime = CampaignTime.Hours(1f);
                break;
            case "partyHasArmy":
                captive.Party._army = ObjectHelper.SkipConstructor<Army>();
                break;
            case "partyHasAttachedTo":
                captive.Party._attachedTo = CreateParty("attached-to-party");
                break;
            case "partyHasAttachedParties":
                captive.Party._attachedParties = new MBList<MobileParty> { CreateParty("attached-party") };
                break;
            case "partyIsAtSea":
                captive.Party._isCurrentlyAtSea = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(guardName));
        }
    }

    private JObject ObserveReadiness() => Parse(DefenderSiegeFixtureCommands.ObserveRosterFixtureReadiness(
        new() { "testclient", "testclient2" }));

    private JObject ObserveReadiness(IObjectManager observationObjects)
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(observationObjects).As<IObjectManager>();
        builder.RegisterInstance(players.Object).As<IPlayerManager>();
        builder.RegisterInstance(synchronization.Object).As<ICampaignSynchronization>();
        IContainer observationContainer = builder.Build();
        ContainerProvider.SetContainer(observationContainer);
        try
        {
            return ObserveReadiness();
        }
        finally
        {
            ContainerProvider.SetContainer(container);
            observationContainer.Dispose();
        }
    }

    private IObjectManager CreateReversePartyIdentityObjectManager()
    {
        var reverseObjects = new Mock<IObjectManager>(MockBehavior.Strict);
        reverseObjects.Setup(manager => manager.TryGetObject<Hero>(It.IsAny<string>(), out It.Ref<Hero>.IsAny))
            .Returns((string id, out Hero hero) => objects.TryGetObject(id, out hero));
        reverseObjects.Setup(manager => manager.TryGetObject<MobileParty>(It.IsAny<string>(), out It.Ref<MobileParty>.IsAny))
            .Returns((string id, out MobileParty party) => objects.TryGetObject(id, out party));
        reverseObjects.Setup(manager => manager.TryGetId(It.IsAny<object>(), out It.Ref<string>.IsAny))
            .Returns((object value, out string id) =>
            {
                if (ReferenceEquals(value, captives[0].Party))
                {
                    id = "reverse-party-identity";
                    return true;
                }
                return objects.TryGetId(value, out id);
            });
        return reverseObjects.Object;
    }

    private static JObject ReadinessRow(JObject result, string controllerId) =>
        result["players"].Values<JObject>().Single(row => row.Value<string>("controllerId") == controllerId);

    private void AssertReadinessObservationIsNonMutating()
    {
        Assert.Empty(actionCalls);
        Assert.Null(RosterFixture.GetValue(null));
        Assert.Null(RosterCaptiveBaselineFixture.GetValue(null));
        foreach (string fieldName in new[] { "pendingCapture", "activeFixture", "restoredFixture" })
            Assert.Null(AccessTools.Field(typeof(DefenderSiegeFixtureCommands), fieldName).GetValue(null));
        snapshots.Verify(service => service.TryCreate(It.IsAny<MobileParty>(), out It.Ref<PartyBehaviorUpdateData>.IsAny), Times.Never);
        AssertNoSnapshotReplay();
    }

    private Settlement CaptureStagingFixture()
    {
        Settlement settlement = PrepareStagingParties();
        AssertSuccess(Parse(DefenderSiegeFixtureCommands.Capture(new() { "testclient", "testclient2" })));
        return settlement;
    }

    private Settlement PrepareStagingParties()
    {
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        settlement.StringId = "castle_ES1";
        settlement.Party = ObjectHelper.SkipConstructor<PartyBase>();
        settlement.Party.Settlement = settlement;
        settlement.Party.ItemRoster = new ItemRoster();
        var town = ObjectHelper.SkipConstructor<Town>();
        town.Owner = settlement.Party;
        settlement.Town = town;
        settlement.SettlementComponent = town;
        Assert.True(objects.AddExisting("castle_ES1", settlement));
        foreach (var captive in captives)
        {
            SetReleased(captive);
            captive.Party.PartyMoveMode = MoveModeType.Hold;
            captive.Party._currentSettlement = settlement;
        }
        return settlement;
    }

    private void InstallLogPatches()
    {
        foreach (string name in new[] { "FixturePrisonerTakenLogPatch", "FixturePrisonerReleasedLogPatch" })
        {
            Type patch = typeof(DefenderSiegeFixtureCommands).GetNestedType(name, BindingFlags.NonPublic);
            Assert.NotNull(patch);
            harmony.CreateClassProcessor(patch).Patch();
        }
        harmony.Patch(AccessTools.Method(typeof(LogEntry), nameof(LogEntry.AddLogEntry), new[] { typeof(LogEntry) }),
            prefix: new HarmonyMethod(typeof(DefenderSiegeFixtureCommandsTests), nameof(CaptureLog)));
    }

    private void AssertScopedLogs(DefaultLogsCampaignBehavior behavior, Hero hero, bool release)
    {
        Captive current = captives.Single(player => player.Hero == hero);
        Captive other = captives.Single(player => player.Hero != hero);
        Logs.Clear();
        if (release)
        {
            behavior.OnHeroPrisonerReleased(hero, current.Captor.Party, null, default, true);
            Assert.Empty(Logs);
            behavior.OnHeroPrisonerReleased(other.Hero, current.Captor.Party, null, default, true);
            behavior.OnHeroPrisonerReleased(hero, other.Captor.Party, null, default, true);
            behavior.OnPrisonerTaken(current.Captor.Party, hero);
            Assert.IsType<TakePrisonerLogEntry>(Logs[2]);
        }
        else
        {
            behavior.OnPrisonerTaken(current.Captor.Party, hero);
            Assert.Empty(Logs);
            behavior.OnPrisonerTaken(current.Captor.Party, other.Hero);
            behavior.OnPrisonerTaken(other.Captor.Party, hero);
            behavior.OnHeroPrisonerReleased(hero, current.Captor.Party, null, default, true);
            Assert.IsType<EndCaptivityLogEntry>(Logs[2]);
        }
        Assert.Equal(3, Logs.Count);
    }

    private static bool CaptureLog(LogEntry logEntry)
    {
        Logs.Add(logEntry);
        return false;
    }

    private void Drift(string drift)
    {
        Captive player = captives[0];
        switch (drift)
        {
            case "player-removed": registrations.Remove(player.Player.ControllerId); break;
            case "player-replaced":
                replacementParty = CreateParty("replacement-party");
                Assert.True(objects.AddExisting(replacementParty.StringId, replacementParty));
                registrations[player.Player.ControllerId] = new Player(player.Player.ControllerId,
                    player.Player.HeroId, replacementParty.StringId, "clan", "character");
                break;
            case "hero-removed": Assert.True(objects.Remove(player.Hero)); break;
            case "hero-rebound":
                Assert.True(objects.Remove(player.Hero));
                Assert.True(objects.AddExisting(player.Player.HeroId, ObjectHelper.SkipConstructor<Hero>()));
                break;
            case "party-removed": Assert.True(objects.Remove(player.Party)); break;
            case "party-rebound":
                Assert.True(objects.Remove(player.Party));
                replacementParty = CreateParty("replacement");
                Assert.True(objects.AddExisting(player.Player.MobilePartyId, replacementParty));
                break;
            default: throw new ArgumentOutOfRangeException(nameof(drift));
        }
    }

    private Captive CreateCaptive(string id)
    {
        var hero = ObjectHelper.SkipConstructor<Hero>();
        hero.StringId = "hero_" + id;
        hero._health = 100;
        hero._characterObject = ObjectHelper.SkipConstructor<CharacterObject>();
        hero.CharacterObject.HeroObject = hero;
        var player = new Captive(new Player(id, hero.StringId, "party_" + id, "clan_" + id, "character_" + id),
            hero, CreateParty("party_" + id), CreateParty("captor_" + id));
        player.Captor.IsActive = true;
        SetCaptured(player);
        Assert.True(objects.AddExisting(player.Player.HeroId, hero));
        Assert.True(objects.AddExisting(player.Player.MobilePartyId, player.Party));
        registrations.Add(id, player.Player);
        var peer = new TestNetwork().CreatePeer($"127.0.0.{peers.Count + 1}");
        Assert.Equal(ConnectionState.Connected, peer.ConnectionState);
        peers.Add(id, peer);
        peerPlayers.Add(peer, player.Player);
        return player;
    }

    private static MobileParty CreateParty(string id)
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.StringId = id;
        party.Party = ObjectHelper.SkipConstructor<PartyBase>();
        party.Party.MobileParty = party;
        party._actualClan = ObjectHelper.SkipConstructor<Clan>();
        party.Party.MemberRoster = new TroopRoster();
        party.Party.PrisonRoster = new TroopRoster();
        party._position = new CampaignVec2(new Vec2(12f, 34f), isOnLand: true);
        party.NavigationTransitionStartTime = CampaignTime.Zero;
        party.Ai = new MobilePartyAi(party);
        return party;
    }

    private static void SetCaptured(Captive player, MobileParty captor = null)
    {
        captor ??= player.Captor;
        player.Hero._heroState = Hero.CharacterStates.Prisoner;
        player.Hero._partyBelongedTo = null;
        player.Hero.PartyBelongedToAsPrisoner = captor.Party;
        player.Party.IsActive = false;
        player.Party._isVisible = false;
        player.Party._partyComponent = null;
        player.Party.Party.MemberRoster = new TroopRoster();
        captor.Party.PrisonRoster = new TroopRoster();
        captor.PrisonRoster.AddToCounts(player.Hero.CharacterObject, 1);
        Visuals.Remove(player.Party.Party);
    }

    private static void SetReleased(Captive player)
    {
        MobileParty captor = player.Hero.PartyBelongedToAsPrisoner?.MobileParty ?? player.Captor;
        player.Hero._heroState = Hero.CharacterStates.Active;
        player.Hero._partyBelongedTo = player.Party;
        player.Hero.PartyBelongedToAsPrisoner = null;
        player.Party.IsActive = true;
        player.Party._isVisible = true;
        var component = ObjectHelper.SkipConstructor<LordPartyComponent>();
        component._leader = player.Hero;
        component.MobileParty = player.Party;
        player.Party._partyComponent = component;
        player.Party.MemberRoster.AddToCounts(player.Hero.CharacterObject, 1);
        captor.Party.PrisonRoster = new TroopRoster();
        if (visualManager != null)
            Visuals[player.Party.Party] = ObjectHelper.SkipConstructor<MobilePartyVisual>();
    }

    private static void ReadVisualManager(ref MobilePartyVisualManager __result) => __result = visualManager;

    private static bool ReadVisual(PartyBase partyBase, ref MobilePartyVisual __result)
    {
        Visuals.TryGetValue(partyBase, out __result);
        return false;
    }

    private static bool ReadHeroIsPrisoner(Hero __instance, ref bool __result)
    {
        if (!PrisonerOverrides.TryGetValue(__instance, out bool isPrisoner)) return true;
        __result = isPrisoner;
        return false;
    }

    private void Capture() => AssertSuccess(CaptureResult());
    private void Normalize() => AssertSuccess(Parse(DefenderSiegeFixtureCommands.NormalizeRosterFixture(new())));
    private static JObject CaptureResult() => Parse(DefenderSiegeFixtureCommands.CaptureRosterFixture(new() { "testclient", "testclient2" }));
    private static JObject PrepareCaptiveBaseline() => Parse(
        DefenderSiegeFixtureCommands.PrepareRosterCaptiveBaseline(new() { "testclient", "testclient2" }));
    private static JObject Restore() => Parse(DefenderSiegeFixtureCommands.RestoreRosterFixture(new()));
    private static JObject Verify() => Parse(DefenderSiegeFixtureCommands.VerifyRosterFixtureRestore(new()));
    private static JObject RestoreCaptiveBaseline() => Parse(
        DefenderSiegeFixtureCommands.RestoreRosterCaptiveBaseline(new()));
    private static JObject VerifyCaptiveBaseline() => Parse(
        DefenderSiegeFixtureCommands.VerifyRosterCaptiveBaselineRestore(new()));
    private static JObject Parse(string result)
    {
        Assert.StartsWith("LIVE_TEST_JSON=", result);
        return JObject.Parse(result.Substring("LIVE_TEST_JSON=".Length));
    }
    private static void AssertSuccess(JObject result) => Assert.True((bool)result["success"], result.ToString());
    private static void AssertRefusal(JObject result, object captured, bool pending = false)
    {
        Assert.False((bool)result["success"], result.ToString());
        Assert.Same(captured, RosterFixture.GetValue(null));
        Assert.Equal(pending, (bool)result["restoredPendingVerification"]);
    }

    private static CampaignTime CreateFutureAttackProtectionDeadline()
    {
        // IsFuture reads the tracker directly, bypassing the bootstrap's CampaignTime.Now stub.
        CampaignTime deadline = Campaign.Current.MapTimeTracker.Now + CampaignTime.Hours(12f);
        Assert.True(deadline.IsFuture, "The test attack-protection deadline must be ahead of the actual tracker.");
        return deadline;
    }

    private static void AssertReleaseAttackProtectionPresent(MobileParty attacker, MobileParty target)
    {
        Assert.Contains(DefaultMobilePartyAIModelPatches.GetPersistedAttackProtections(), protection =>
            ReferenceEquals(protection.AttackerParty, attacker) && ReferenceEquals(protection.TargetParty, target) &&
            protection.DisabledUntil.IsFuture);
    }

    private static void AssertNoAttackProtection(MobileParty attacker, MobileParty target)
    {
        Assert.DoesNotContain(DefaultMobilePartyAIModelPatches.GetPersistedAttackProtections(), protection =>
            ReferenceEquals(protection.AttackerParty, attacker) && ReferenceEquals(protection.TargetParty, target));
    }

    private static object ReadState(Captive player) => new
    {
        player.Hero.HeroState,
        player.Hero.PartyBelongedTo,
        player.Hero.PartyBelongedToAsPrisoner,
        player.Hero.CaptivityStartTime,
        PartyState = ReadPartyState(player.Party)
    };

    private static object ReadPartyState(MobileParty party) => new
    {
        party.IsActive,
        party.IsVisible,
        party.Position,
        party.Bearing,
        party.LeaderHero,
        Members = party.MemberRoster.TotalManCount,
        Prisoners = party.PrisonRoster.TotalManCount
    };
    private void AssertNoSnapshotReplay() => snapshots.Verify(service => service.TryApply(
        It.IsAny<MobileParty>(), It.IsAny<PartyBehaviorUpdateData>(), out It.Ref<IInteractablePoint>.IsAny), Times.Never);

    public void Dispose()
    {
        harmony.UnpatchAll(harmony.Id);
        foreach (Captive captive in captives ?? Array.Empty<Captive>())
        {
            DefaultMobilePartyAIModelPatches.RemoveAttackProtectionsForParty(captive.Party);
            DefaultMobilePartyAIModelPatches.RemoveAttackProtectionsForParty(captive.Captor);
        }
        Visuals.Clear();
        visualManager = null;
        Logs.Clear();
        PrisonerOverrides.Clear();
        foreach (var previous in previousFixtures) previous.Key.SetValue(null, previous.Value);
        ContainerProvider.SetContainer(previousContainer);
        container?.Dispose();
        Campaign.Current = previousCampaign;
        ModInformation.IsServer = previousServer;
    }

    private sealed record Captive(Player Player, Hero Hero, MobileParty Party, MobileParty Captor);
}
#endif
