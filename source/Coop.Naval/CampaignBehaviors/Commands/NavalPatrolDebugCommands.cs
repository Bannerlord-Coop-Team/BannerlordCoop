using Common;
using Common.Commands;
using GameInterface.Services.ObjectManager;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Naval.CampaignBehaviors.Commands;

internal class NavalPatrolDebugCommands
{
    private const string CommandPrefix = "coop.debug.naval";

    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    // coop.debug.naval.patrol_status town_ES1
    public sealed class PatrolStatusCoopCommand : ICoopCommand
    {
        private readonly IObjectManager objectManager;

        public PatrolStatusCoopCommand(IObjectManager objectManager)
        {
            this.objectManager = objectManager;
        }

        public string Prefix => CommandPrefix;
        public string Name => "patrol_status";
        public string Description => "Lists the coastal patrol queue, patrol party and status of port towns, run on the server and a client to compare.";
        public CoopCommandSide Side => CoopCommandSide.Both;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlementId", "Settlement id, defaults to every port town.", isRequired: false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var behavior = Campaign.Current?.GetCampaignBehavior<NavalPatrolPartiesCampaignBehavior>();
            if (behavior == null) return Failed("NavalPatrolPartiesCampaignBehavior is not available.");

            var settlements = Settlement.All.Where(settlement => settlement.IsTown && settlement.HasPort);
            if (args.Count > 0)
            {
                if (!objectManager.TryGetObject<Settlement>(args[0], out var settlement)) return Failed($"Settlement with id {args[0]} not found.");

                settlements = new[] { settlement };
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Coastal patrols on {(ModInformation.IsServer ? "SERVER" : "CLIENT")}:");

            foreach (var settlement in settlements)
            {
                var queue = behavior._partyGenerationQueue.TryGetValue(settlement, out var queueTime) ? queueTime.ToString() : "none";
                var patrol = behavior.GetNavalPatrolParty(settlement);
                var patrolId = patrol == null ? "none" : objectManager.TryGetId(patrol, out var id) ? id : "<unregistered>";

                sb.AppendLine($"{settlement.StringId} ({settlement.Name}) queue={queue} patrol={patrolId} status={behavior.GetSettlementPatrolStatus(settlement)}");
            }

            return Succeeded(sb.ToString());
        }
    }

    // coop.debug.naval.patrol_tick town_ES1
    public sealed class PatrolTickCoopCommand : ICoopCommand
    {
        private readonly IObjectManager objectManager;

        public PatrolTickCoopCommand(IObjectManager objectManager)
        {
            this.objectManager = objectManager;
        }

        public string Prefix => CommandPrefix;
        public string Name => "patrol_tick";
        public string Description => "Runs the coastal patrol daily tick for one settlement, which queues or spawns its patrol.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlementId", "Settlement id from coop.debug.naval.patrol_status."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");
            if (args.Count != 1) return Failed("Expected a settlement id.");

            var behavior = Campaign.Current?.GetCampaignBehavior<NavalPatrolPartiesCampaignBehavior>();
            if (behavior == null) return Failed("NavalPatrolPartiesCampaignBehavior is not available.");

            if (!objectManager.TryGetObject<Settlement>(args[0], out var settlement)) return Failed($"Settlement with id {args[0]} not found.");

            behavior.DailyTickSettlement(settlement);

            return Succeeded($"Ticked {settlement.StringId}, status={behavior.GetSettlementPatrolStatus(settlement)}");
        }
    }
}
