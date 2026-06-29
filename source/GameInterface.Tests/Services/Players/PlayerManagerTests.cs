using Common;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using Moq;
using Serilog;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.Players;

public class PlayerManagerTests
{
    private const string ControllerId = "PlayerOne";
    private const string PartyId = "PartyOne";

    static PlayerManagerTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void AddPlayer_MobileParty_InvalidatesBaseSpeedCache()
    {
        Assert.True(GameThread.Instance.IsInitialized, "game-loop pump was not initialized");

        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party._partyPureSpeedLastCheckVersion = 42;

        var objectManager = new Mock<IObjectManager>();
        var controllerIdProvider = new ControllerIdProvider();
        MobileParty resolvedParty = party;
        objectManager.Setup(o => o.TryGetObjectWithLogging<MobileParty>(PartyId, out resolvedParty))
            .Returns(true);

        var playerManager = new PlayerManager(
            new Mock<ILogger>().Object,
            objectManager.Object,
            controllerIdProvider);

        var playerObjects = GetPlayerObjects();
        try
        {
            Assert.True(playerManager.AddPlayer(new Player(
                ControllerId,
                string.Empty,
                PartyId,
                string.Empty,
                string.Empty)));
            Assert.True(PlayerManager.TryGetControlledObjectInfo(party, out _));
            Assert.True(
                SpinWait.SpinUntil(() => party._partyPureSpeedLastCheckVersion == -1, TimeSpan.FromSeconds(5)),
                "player-party speed cache was not invalidated");
        }
        finally
        {
            playerObjects.Remove(party);
        }
    }

    private static ConditionalWeakTable<object, ControlledObjectInfo> GetPlayerObjects() =>
        (ConditionalWeakTable<object, ControlledObjectInfo>)AccessTools
            .Field(typeof(PlayerManager), "PlayerObjects")
            .GetValue(null)!;
}