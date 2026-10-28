using Common;
using Common.Commands;
using GameInterface.Services.Ships;
using Missions.Battles;
using Newtonsoft.Json;
using System;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>Reads accepted server ship reports without requiring a mission or consuming pending campaign fates.</summary>
public interface INavalServerHpCoopCommand : ICoopCommand { }

/// <inheritdoc cref="INavalServerHpCoopCommand"/>
public sealed class NavalServerHpCoopCommand : INavalServerHpCoopCommand
{
    private readonly IBattleShipFateLedger ledger;
    private readonly INavalShipEngine engine;

    // Uses the same session ledger that accepts battle clients' fate and capture reports.
    public NavalServerHpCoopCommand(IBattleShipFateLedger ledger, INavalShipEngine engine)
    {
        this.ledger = ledger;
        this.engine = engine;
    }

    public string Prefix => "coop.debug.naval";
    public string Name => "server_hp";
    public string Description => "Reports accepted pending server fates, or client mission HP with campaign ids for comparison.";
    public CoopCommandSide Side => CoopCommandSide.Both;
    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    // Console commands run on the game thread, ordered with the handler's ledger writes.
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (args == null || args.Count != 0)
            return new CoopCommandResult(false, "Usage: coop.debug.naval.server_hp", "command_failed");

        if (ModInformation.IsClient) return InspectClient();

        return new CoopCommandResult(true, "NAVAL_SERVER_HP " + JsonConvert.SerializeObject(ledger.Inspect()));
    }

    // Maps each client's mission hull to the campaign id used by the server ledger, including after hull swaps.
    private CoopCommandResult InspectClient()
    {
        var controller = Mission.Current?.GetMissionBehavior<CoopBattleController>();
        if (controller == null)
            return new CoopCommandResult(false, "No active coop battle mission.", "command_failed");

        var state = new
        {
            instance = controller.Session.InstanceId,
            ships = controller.MissionComponent.ShipRegistry.Ships.Select(ship => new
            {
                shipId = ship.ShipId,
                campaignShipId = engine.CampaignShipIdOf(ship.Hull),
                hitPoints = engine.ReadCondition(ship.Hull)?.HitPoints,
                currentAuthority = ship.CurrentAuthority,
            }).ToArray(),
        };
        return new CoopCommandResult(true, "NAVAL_CLIENT_HP " + JsonConvert.SerializeObject(state));
    }
}
