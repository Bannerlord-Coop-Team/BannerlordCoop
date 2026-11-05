using E2E.Tests.Environment.Instance;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>The server applies a surrender only for the sending player's own party.</summary>
public class PlayerSurrenderSenderTests : MissionTestEnvironment
{
    public PlayerSurrenderSenderTests(ITestOutputHelper output) : base(output, numClients: 2) { }

    private EnvironmentInstance ClientA => Clients.ElementAt(0);
    private EnvironmentInstance ClientB => Clients.ElementAt(1);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Surrender_IsAppliedOnlyForTheSendersParty(bool fromOwner)
    {
        var (mapEventId, partyIds) = SetupCoopBattle("ctrl-A", "ctrl-B");
        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            playerManager.SetPeer("ctrl-A", ClientA.NetPeer);
            playerManager.SetPeer("ctrl-B", ClientB.NetPeer);
        });

        Server.SimulateMessage((fromOwner ? ClientA : ClientB).NetPeer, new NetworkPlayerSurrendered(partyIds[0], mapEventId));
        Server.PumpGameThread();

        // The owner's surrender ends the battle, which removes the map event.
        Server.Call(() => Assert.Equal(!fromOwner,
            Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent) && !mapEvent.AttackerSide.IsSurrendered));
    }
}
