#if DEBUG
using Common;
using Common.Commands;
using Common.Network;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Commands;

public sealed class Issue1837RouteEnemiesCoopCommand : ICoopCommand
{
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly INetwork network;

    public Issue1837RouteEnemiesCoopCommand(
        IObjectManager objectManager, IPlayerManager playerManager, INetwork network)
    {
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.network = network;
    }

    public string Prefix => "coop.debug.siege";
    public string Name => "issue1837_route_enemies";
    public string Description => "Orders the battle authority to route enemies in the selected player-led siege.";
    public CoopCommandSide Side => CoopCommandSide.Server;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("mapEventId", "The registered siege assault id."),
        new ExpectedArgs("leaderControllerId", "The connected lead besieger controller id."),
        new ExpectedArgs("settlementId", "The captured settlement target id."),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!ModInformation.IsServer)
            return new CoopCommandResult(false, "Run this command on the server.", "command_failed");
        if (!playerManager.TryGetPlayer(args[1], out var player) || !playerManager.IsConnected(player) ||
            !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var leader) ||
            !objectManager.TryGetObject<MapEvent>(args[0], out var mapEvent))
            return new CoopCommandResult(false, "The selected player or siege is unavailable.", "command_failed");
        if (mapEvent.IsFinalized || !mapEvent.IsSiegeAssault || mapEvent.HasWinner ||
            mapEvent.MapEventSettlement?.StringId != args[2] ||
            mapEvent.AttackerSide?.LeaderParty?.MobileParty != leader || leader.MapEvent != mapEvent)
            return new CoopCommandResult(false, "The selected battle is not the active player-led target siege.", "command_failed");

        // The existing mission handler routes only the elected authority's matching siege mission.
        network.SendAll(new NetworkRouteBattleEnemies(args[0], enemiesToLeaveFighting: 0));
        return new CoopCommandResult(true, "Enemy rout requested; battle completion must be observed separately.");
    }
}
#endif
