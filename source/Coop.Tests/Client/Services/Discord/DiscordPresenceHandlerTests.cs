using Common.Messaging;
using Common.Network.Messages;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Services.Discord;
using Coop.Core.Common.Services.Connection.Messages;
using GameInterface.Services.GameState.Messages;
using Missions.Messages;
using Moq;
using System;
using Xunit;

namespace Coop.Tests.Client.Services.Discord;

public class DiscordPresenceHandlerTests : IDisposable
{
    private readonly MessageBroker broker = new MessageBroker();
    private readonly Mock<IDiscordPresenceClient> client = new Mock<IDiscordPresenceClient>();
    private readonly DiscordPresenceHandler handler;
    private readonly DateTime startedAt = new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc);
    private DateTime now;

    public DiscordPresenceHandlerTests()
    {
        now = startedAt;
        handler = new DiscordPresenceHandler(broker, client.Object, () => now);
    }

    [Fact]
    public void ConnectionAndLoading_DoNotAdvertiseUntilCampaignJoinCompletes()
    {
        broker.Publish(this, new ClientCampaignReady());
        broker.Publish(this, new BattleMissionReady("battle"));
        broker.Publish(this, new NetworkConnected());
        broker.Publish(this, new NetworkConnectedPlayersChanged(3));
        client.VerifyNoOtherCalls();

        broker.Publish(this, new ClientCampaignReady());

        client.Verify(c => c.SetPresence("In a co-op campaign", "3 players", startedAt), Times.Once);
    }

    [Theory]
    [InlineData(1, "1 player")]
    [InlineData(3, "3 players")]
    [InlineData(0, "1 player")]
    [InlineData(-1, "1 player")]
    public void PlayerCount_UsesServerClientCountWithoutAddingSelfOrHost(int count, string text)
    {
        broker.Publish(this, new NetworkConnected());
        broker.Publish(this, new NetworkConnectedPlayersChanged(count));
        broker.Publish(this, new ClientCampaignReady());

        client.Verify(c => c.SetPresence("In a co-op campaign", text, startedAt), Times.Once);
    }

    [Fact]
    public void BattleTransitionsAndRosterChanges_KeepConnectionTimestamp()
    {
        EnterCampaign();
        now = startedAt.AddMinutes(10);
        broker.Publish(this, new NetworkConnected());
        broker.Publish(this, new BattleMissionReady("battle"));
        broker.Publish(this, new NetworkConnectedPlayersChanged(3));
        broker.Publish(this, new BattleMissionEnded("battle"));

        client.Verify(c => c.SetPresence("Fighting a battle", "1 player", startedAt), Times.Once);
        client.Verify(c => c.SetPresence("Fighting a battle", "3 players", startedAt), Times.Once);
        client.Verify(c => c.SetPresence("In a co-op campaign", "3 players", startedAt), Times.Once);
    }

    [Fact]
    public void DuplicateEventsAndUnrelatedBattleEnd_DoNotRepublish()
    {
        EnterCampaign();
        broker.Publish(this, new ClientCampaignReady());
        broker.Publish(this, new NetworkConnectedPlayersChanged(1));
        broker.Publish(this, new BattleMissionReady("battle"));
        broker.Publish(this, new BattleMissionReady("battle"));
        broker.Publish(this, new BattleMissionEnded("old-battle"));

        client.Verify(c => c.SetPresence("In a co-op campaign", "1 player", startedAt), Times.Once);
        client.Verify(c => c.SetPresence("Fighting a battle", "1 player", startedAt), Times.Once);
        client.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("disconnect")]
    [InlineData("end")]
    [InlineData("menu")]
    public void SessionEnd_ClearsBattleAndIgnoresLateEventsUntilNewConnection(string reason)
    {
        EnterCampaign();
        broker.Publish(this, new BattleMissionReady("battle"));
        switch (reason)
        {
            case "disconnect": broker.Publish(this, new NetworkDisconnected(default)); break;
            case "end": broker.Publish(this, new EndCoopMode()); break;
            case "menu": broker.Publish(this, new MainMenuEntered()); break;
        }
        client.Verify(c => c.ClearPresence(), Times.Once);
        client.Invocations.Clear();
        broker.Publish(this, new NetworkConnectedPlayersChanged(8));
        broker.Publish(this, new BattleMissionEnded("battle"));
        broker.Publish(this, new ClientCampaignReady());
        broker.Publish(this, new BattleMissionReady("late-battle"));
        client.VerifyNoOtherCalls();

        now = startedAt.AddHours(1);
        EnterCampaign();
        client.Verify(c => c.SetPresence("In a co-op campaign", "1 player", now), Times.Once);
    }

    [Fact]
    public void DisconnectAndDispose_ClearOnlyOnceAndUnsubscribe()
    {
        EnterCampaign();
        broker.Publish(this, new NetworkDisconnected(default));
        broker.Publish(this, new EndCoopMode());
        handler.Dispose();
        handler.Dispose();
        EnterCampaign();
        broker.Publish(this, new BattleMissionReady("battle"));

        client.Verify(c => c.ClearPresence(), Times.Once);
        client.Verify(c => c.SetPresence("In a co-op campaign", "1 player", startedAt), Times.Once);
        client.VerifyNoOtherCalls();
    }

    [Fact]
    public void DisposeActiveSession_ClearsPresence()
    {
        EnterCampaign();
        handler.Dispose();
        client.Verify(c => c.ClearPresence(), Times.Once);
    }

    [Fact]
    public void DisposeFailedJoin_DoesNotStartDiscord()
    {
        broker.Publish(this, new NetworkConnected());
        handler.Dispose();
        client.VerifyNoOtherCalls();
    }

    private void EnterCampaign()
    {
        broker.Publish(this, new NetworkConnected());
        broker.Publish(this, new ClientCampaignReady());
    }

    public void Dispose()
    {
        handler.Dispose();
        broker.Dispose();
    }
}
