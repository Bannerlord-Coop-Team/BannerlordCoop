using Common;
using Common.Commands;
using GameInterface.Services.Settlements.Interfaces;
using GameInterface.Services.Villages.Commands;
using GameInterface.Utils.Commands;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Party.Commands;

/// <summary>Debug commands that take a player's party from a port out to sea and sail it into another port.</summary>
public class PartySailCommands
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    // coop.debug.naval.set_sail PlayerOne
    /// <summary>Sets sail from the port a player's party is in, through the server's leave and set sail calls.</summary>
    public sealed class SetSailCoopCommand : ICoopCommand
    {
        private readonly ISettlementInterface settlementInterface;

        /// <summary>Uses the settlement interface the server's set sail leave goes through.</summary>
        public SetSailCoopCommand(ISettlementInterface settlementInterface)
        {
            this.settlementInterface = settlementInterface;
        }

        public string Prefix => "coop.debug.naval";

        public string Name => "set_sail";

        public string Description => "Sets sail from the port a player's party is in; its fleet must be anchored there.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The player whose party sets sail.", true),
        };

        /// <summary>Checks vanilla's Set sail rule for the party's port, then leaves it at sea in front of the port.</summary>
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsNavalEnabled)
                return Failed("NavalDLC is not active on the server.");

            if (!MapEventDebugCommands.TryGetPlayerParty(args[0], requireReady: true, out _, out var party, out var error))
                return Failed(error);

            var port = party.CurrentSettlement;
            if (port == null || !port.HasPort)
                return Failed($"{party.StringId} is not in a settlement with a port.");

            // These two checks are vanilla NavalTransitionCampaignBehavior.GetIsSetSailEnabled
            if (party.Army != null && party.Army.LeaderParty != party)
                return Failed($"{party.StringId} is following an army and can't set sail.");
            if (!party.HasNavalNavigationCapability || !party.Anchor.IsAtSettlement(port))
                return Failed($"{party.StringId} has no ships anchored at {port.StringId}.");

            // The leave and set sail calls of ServerSettlementExitEnterHandler's SetSail leave, without its encounter-leave event;
            // the replicated leave also closes the owner's port menu
            settlementInterface.PartyLeaveSettlement(party);
            settlementInterface.PartySetSail(party, port);

            return Succeeded($"{party.StringId} set sail from {port.StringId} with {party.Ships.Count} ship(s).");
        }
    }

    // coop.debug.naval.sail_to PlayerOne town_ES2
    /// <summary>Orders a player's party at sea into a port, the same order a map click on that port gives.</summary>
    public sealed class SailToCoopCommand : ICoopCommand
    {
        private readonly ICommandObjectResolver resolver;

        /// <summary>Uses the resolver to find the port by id or name.</summary>
        public SailToCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.naval";

        public string Name => "sail_to";

        public string Description => "Sails a player's party at sea into a port; it enters the settlement when it arrives.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The player whose party sails.", true),
            new ExpectedArgs("settlement", "The settlement with a port to sail into, by id or name.", true),
        };

        /// <summary>Sets the party's move order to the port; the movement patches replicate it to every client.</summary>
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsNavalEnabled)
                return Failed("NavalDLC is not active on the server.");

            if (!MapEventDebugCommands.TryGetPlayerParty(args[0], requireReady: true, out _, out var party, out var error))
                return Failed(error);

            if (!resolver.TryResolveSettlement(args[1], out var port, out error))
                return Failed(error);

            if (!port.HasPort)
                return Failed($"{port.StringId} has no port.");

            // Vanilla EnterSettlementAction keeps a party with no land navigation at sea inside a port
            if (party.CurrentSettlement != null)
                return Failed($"{party.StringId} is in {party.CurrentSettlement.StringId}, set sail first.");

            if (!party.IsCurrentlyAtSea)
                return Failed($"{party.StringId} is not at sea, set sail first.");

            // Same call as vanilla SettlementVisual.OnMapClick for a port reached by sea
            party.SetMoveGoToSettlement(port, MobileParty.NavigationType.Naval, isTargetingThePort: true);

            return Succeeded($"{party.StringId} is sailing to {port.StringId}.");
        }
    }
}
