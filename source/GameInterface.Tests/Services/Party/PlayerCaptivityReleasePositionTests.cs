using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEventParties;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party.Data;
using GameInterface.Services.Party.Handlers;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using GameInterface.Surrogates;
using HarmonyLib;
using Moq;
using ProtoBuf.Meta;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Party;

public class PlayerCaptivityReleasePositionTests
{
    private readonly Mock<IMessageBroker> messageBroker = new();
    private readonly Mock<IObjectManager> objectManager = new();
    private readonly Mock<INetwork> network = new();
    private readonly Mock<ITroopRosterInterface> troopRosterInterface = new();
    private readonly PartyDoneLogicHandler handler;

    public PlayerCaptivityReleasePositionTests()
    {
        new SurrogateCollection();
        handler = new PartyDoneLogicHandler(
            messageBroker.Object,
            objectManager.Object,
            network.Object,
            troopRosterInterface.Object);
    }

    [Fact]
    public void CreatePlayerCaptivityReleaseEvents_PlayerPrisonerRemoved_CreatesReleaseAtReleaserPosition()
    {
        var playerHero = ObjectHelper.SkipConstructor<Hero>();
        var playerCharacter = ObjectHelper.SkipConstructor<CharacterObject>();
        playerCharacter.HeroObject = playerHero;
        var releasePosition = Position(72.5f, 18.25f);
        var leftPrisonerDelta = new TroopRosterData(new[]
        {
            new TroopRosterElementData("player-character", -1, 0, 0),
        });
        var rightPrisonerDelta = EmptyRosterData();

        SetupObject("player-character", playerCharacter);
        MarkPlayerHero(playerHero, "player-hero");
        try
        {
            var releaseEvents = handler.CreatePlayerCaptivityReleaseEvents(
                leftPrisonerDelta,
                rightPrisonerDelta,
                true,
                releasePosition,
                out var filteredLeftPrisonerDelta,
                out var filteredRightPrisonerDelta);

            var releaseEvent = Assert.Single(releaseEvents);
            Assert.Same(playerHero, releaseEvent.PrisonerHero);
            Assert.Equal(EndCaptivityDetail.ReleasedByChoice, releaseEvent.Detail);
            Assert.True(releaseEvent.HasReleasePosition);
            AssertPosition(releasePosition, releaseEvent.ReleasePosition);
            Assert.Empty(filteredLeftPrisonerDelta.Data);
            Assert.Empty(filteredRightPrisonerDelta.Data);
        }
        finally
        {
            GetPlayerObjects().Remove(playerHero);
        }
    }

    [Fact]
    public void CreatePlayerCaptivityReleaseEvents_NonPlayerPrisonerRemoved_KeepsRosterDelta()
    {
        var nonPlayerHero = ObjectHelper.SkipConstructor<Hero>();
        var nonPlayerCharacter = ObjectHelper.SkipConstructor<CharacterObject>();
        nonPlayerCharacter.HeroObject = nonPlayerHero;
        var releasePosition = Position(10f, 11f);
        var leftPrisonerDelta = new TroopRosterData(new[]
        {
            new TroopRosterElementData("non-player-character", -1, 0, 0),
        });

        SetupObject("non-player-character", nonPlayerCharacter);

        var releaseEvents = handler.CreatePlayerCaptivityReleaseEvents(
            leftPrisonerDelta,
            EmptyRosterData(),
            true,
            releasePosition,
            out var filteredLeftPrisonerDelta,
            out _);

        Assert.Empty(releaseEvents);
        var keptElement = Assert.Single(filteredLeftPrisonerDelta.Data);
        Assert.Equal("non-player-character", keptElement.CharacterId);
        Assert.Equal(-1, keptElement.Number);
    }

    [Fact]
    public void CreatePlayerCaptivityReleaseEvents_PlayerPrisonerTransferred_KeepsRosterDelta()
    {
        var playerHero = ObjectHelper.SkipConstructor<Hero>();
        var playerCharacter = ObjectHelper.SkipConstructor<CharacterObject>();
        playerCharacter.HeroObject = playerHero;
        var releasePosition = Position(1f, 2f);
        var leftPrisonerDelta = new TroopRosterData(new[]
        {
            new TroopRosterElementData("player-character", -1, 0, 0),
        });
        var rightPrisonerDelta = new TroopRosterData(new[]
        {
            new TroopRosterElementData("player-character", 1, 0, 0),
        });

        SetupObject("player-character", playerCharacter);
        MarkPlayerHero(playerHero, "player-hero");
        try
        {
            var releaseEvents = handler.CreatePlayerCaptivityReleaseEvents(
                leftPrisonerDelta,
                rightPrisonerDelta,
                true,
                releasePosition,
                out var filteredLeftPrisonerDelta,
                out var filteredRightPrisonerDelta);

            Assert.Empty(releaseEvents);
            Assert.Equal(-1, Assert.Single(filteredLeftPrisonerDelta.Data).Number);
            Assert.Equal(1, Assert.Single(filteredRightPrisonerDelta.Data).Number);
        }
        finally
        {
            GetPlayerObjects().Remove(playerHero);
        }
    }

    [Fact]
    public void CreatePlayerCaptivityReleaseEvents_PlayerPrisonerDiscardedFromRight_CreatesRelease()
    {
        var playerHero = ObjectHelper.SkipConstructor<Hero>();
        var playerCharacter = ObjectHelper.SkipConstructor<CharacterObject>();
        playerCharacter.HeroObject = playerHero;
        var releasePosition = Position(3f, 4f);
        var leftPrisonerDelta = new TroopRosterData(new[]
        {
            new TroopRosterElementData("player-character", 1, 0, 0),
        });
        var rightPrisonerDelta = new TroopRosterData(new[]
        {
            new TroopRosterElementData("player-character", -1, 0, 0),
        });

        SetupObject("player-character", playerCharacter);
        MarkPlayerHero(playerHero, "player-hero");
        try
        {
            var releaseEvents = handler.CreatePlayerCaptivityReleaseEvents(
                leftPrisonerDelta,
                rightPrisonerDelta,
                false,
                releasePosition,
                out var filteredLeftPrisonerDelta,
                out var filteredRightPrisonerDelta);

            var releaseEvent = Assert.Single(releaseEvents);
            Assert.Same(playerHero, releaseEvent.PrisonerHero);
            Assert.True(releaseEvent.HasReleasePosition);
            AssertPosition(releasePosition, releaseEvent.ReleasePosition);
            Assert.Equal(1, Assert.Single(filteredLeftPrisonerDelta.Data).Number);
            Assert.Empty(filteredRightPrisonerDelta.Data);
        }
        finally
        {
            GetPlayerObjects().Remove(playerHero);
        }
    }

    [Fact]
    public void CreatePlayerCaptivityReleaseEvents_PlayerPrisonerTransferredFromRightToLeft_KeepsRosterDelta()
    {
        var playerHero = ObjectHelper.SkipConstructor<Hero>();
        var playerCharacter = ObjectHelper.SkipConstructor<CharacterObject>();
        playerCharacter.HeroObject = playerHero;
        var releasePosition = Position(5f, 6f);
        var leftPrisonerDelta = new TroopRosterData(new[]
        {
            new TroopRosterElementData("player-character", 1, 0, 0),
        });
        var rightPrisonerDelta = new TroopRosterData(new[]
        {
            new TroopRosterElementData("player-character", -1, 0, 0),
        });

        SetupObject("player-character", playerCharacter);
        MarkPlayerHero(playerHero, "player-hero");
        try
        {
            var releaseEvents = handler.CreatePlayerCaptivityReleaseEvents(
                leftPrisonerDelta,
                rightPrisonerDelta,
                true,
                releasePosition,
                out var filteredLeftPrisonerDelta,
                out var filteredRightPrisonerDelta);

            Assert.Empty(releaseEvents);
            Assert.Equal(1, Assert.Single(filteredLeftPrisonerDelta.Data).Number);
            Assert.Equal(-1, Assert.Single(filteredRightPrisonerDelta.Data).Number);
        }
        finally
        {
            GetPlayerObjects().Remove(playerHero);
        }
    }

    [Fact]
    public void NetworkCompleteDoneLogic_RoundTrip_PreservesReleaserPartyPosition()
    {
        var releasePosition = Position(44.75f, 91.125f);
        var original = new NetworkCompleteDoneLogic(
            "main-hero",
            Array.Empty<FlattenedTroop>(),
            Array.Empty<FlattenedTroop>(),
            Array.Empty<FlattenedTroop>(),
            EmptyRosterData(),
            EmptyRosterData(),
            EmptyRosterData(),
            EmptyRosterData(),
            Array.Empty<ItemRosterElement>(),
            new UpgradedTroopHistoryData(new List<UpgradedTroopHistoryElementData>()),
            string.Empty,
            string.Empty,
            0,
            0,
            0,
            true,
            releasePosition,
            Helpers.PartyScreenHelper.PartyScreenMode.Normal,
            new TroopRosterOrderData(new()));

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            RuntimeTypeModel.Default.Serialize(ms, original);
            bytes = ms.ToArray();
        }

        NetworkCompleteDoneLogic result;
        using (var ms = new MemoryStream(bytes))
        {
            result = (NetworkCompleteDoneLogic)RuntimeTypeModel.Default.Deserialize(ms, null, typeof(NetworkCompleteDoneLogic));
        }

        AssertPosition(releasePosition, result.ReleaserPartyPosition);
    }

    private void SetupObject<T>(string id, T obj)
    {
        objectManager.Setup(o => o.TryGetObjectWithLogging<T>(id, out obj)).Returns(true);
    }

    private void MarkPlayerHero(Hero hero, string heroId)
    {
        SetupObject(heroId, hero);
        var playerManager = new PlayerManager(Mock.Of<ILogger>(), objectManager.Object, new ControllerIdProvider());
        playerManager.AddPlayer(new Player("controller-1", heroId, string.Empty, string.Empty, string.Empty));
    }

    private static TroopRosterData EmptyRosterData() =>
        new(Array.Empty<TroopRosterElementData>());

    private static CampaignVec2 Position(float x, float y) =>
        new(new Vec2(x, y), true);

    private static void AssertPosition(CampaignVec2 expected, CampaignVec2 actual)
    {
        Assert.Equal(expected.X, actual.X);
        Assert.Equal(expected.Y, actual.Y);
        Assert.Equal(expected.IsOnLand, actual.IsOnLand);
    }

    private static ConditionalWeakTable<object, ControlledObjectInfo> GetPlayerObjects() =>
        (ConditionalWeakTable<object, ControlledObjectInfo>)AccessTools
            .Field(typeof(PlayerManager), "PlayerObjects")
            .GetValue(null)!;
}
