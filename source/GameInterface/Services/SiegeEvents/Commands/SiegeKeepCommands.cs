using Common;
using Common.Commands;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEvents.Handlers;
using GameInterface.Utils.Commands;
using System.Linq;
using TaleWorlds.Core;

namespace GameInterface.Services.SiegeEvents.Commands;

/// <summary>Stages the keep (lord's hall) battle of a running siege assault.</summary>
internal static class SiegeKeepCommands
{
    // coop.debug.siege.open_keep castle_ES1
    public sealed class OpenKeepCoopCommand : ICoopCommand
    {
        private readonly ICommandObjectResolver resolver;
        private readonly IObjectManager objectManager;
        private readonly SiegeKeepHandler keepHandler;
        private readonly ISiegeKeepTroopSelection keepSelection;

        public OpenKeepCoopCommand(ICommandObjectResolver resolver, IObjectManager objectManager,
            SiegeKeepHandler keepHandler, ISiegeKeepTroopSelection keepSelection)
        {
            this.resolver = resolver;
            this.objectManager = objectManager;
            this.keepHandler = keepHandler;
            this.keepSelection = keepSelection;
        }

        public string Prefix => "coop.debug.siege";
        public string Name => "open_keep";
        public string Description => "Opens the keep stage of a siege assault as if its defenders just fell back from the walls.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlementId", "Besieged settlement name or id; quote names with spaces."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsServer) return Failed("This command can only be used by the server");
            if (!resolver.TryResolveSettlement(args[0], out var settlement, out var error)) return Failed(error);

            var mapEvent = settlement.Party.MapEvent;
            if (mapEvent?.IsSiegeAssault != true || mapEvent.IsFinalized || mapEvent.BattleState != BattleState.None)
                return Failed($"{settlement.Name} has no running siege assault.");
            if (!objectManager.TryGetId(mapEvent, out var mapEventId)) return Failed("The siege assault is not registered.");
            if (ServerBattleModeArbiter.IsClaimed(mapEventId))
                return Failed("The siege assault is still being fought; end its walls mission first.");
            if (keepSelection.IsOpen(mapEvent)) return Failed("The keep stage is already open.");
            if (!keepHandler.TryOpenStage(mapEvent))
                return Failed("The keep stage needs an attacking player, no defending player, and defenders left.");

            var seats = keepSelection.GetSeats(mapEvent)
                .Select(seat => $"{seat.ControllerId}:{seat.MobilePartyId}+{seat.EscortAllowance}");
            return Succeeded($"Opened the keep stage of {settlement.StringId} for map event {mapEventId}: {string.Join(", ", seats)}");
        }
    }

    private static CoopCommandResult Succeeded(string output) => new(true, output);

    private static CoopCommandResult Failed(string output) => new(false, output, "command_failed");
}
