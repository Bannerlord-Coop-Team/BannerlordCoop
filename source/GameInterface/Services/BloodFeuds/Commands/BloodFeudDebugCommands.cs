using Common;
using Common.Commands;
using GameInterface.CoopSessionData;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Utils.Commands;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.BloodFeuds.Commands;

/// <summary>
/// Console commands that stage and inspect blood feuds for live tests.
/// </summary>
internal class BloodFeudDebugCommands
{
    private static CoopCommandResult Succeeded(string output) => new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");

    // coop.debug.blood_feud.state
    /// <summary>
    /// Lists the clans flagged as feuding with this instance's player clan, and on the server the per player clan feud data.
    /// </summary>
    public sealed class BloodFeudStateCoopCommand : ICoopCommand
    {
        private readonly ICoopSessionProvider coopSessionProvider;

        public BloodFeudStateCoopCommand(ICoopSessionProvider coopSessionProvider)
        {
            this.coopSessionProvider = coopSessionProvider;
        }

        public string Prefix => "coop.debug.blood_feud";
        public string Name => "state";
        public string Description => "Lists local clan blood feud flags, and the server's per player clan blood feud data.";
        public CoopCommandSide Side => CoopCommandSide.Both;
        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"MainHero={Hero.MainHero?.StringId} PlayerClan={Clan.PlayerClan?.StringId}");
            sb.AppendLine("Local clan flags (HasBloodFeudWithPlayer or nonzero counts):");
            foreach (var clan in Clan.All.Where(c => c.HasBloodFeudWithPlayer || c.BloodFeudExecutionsDoneCount != 0 || c.BloodFeudExecutionsReceivedCount != 0))
            {
                sb.AppendLine($"  {clan.StringId} ({clan.Name}) feud={clan.HasBloodFeudWithPlayer} done={clan.BloodFeudExecutionsDoneCount} received={clan.BloodFeudExecutionsReceivedCount}");
            }

            if (ModInformation.IsServer)
            {
                var data = coopSessionProvider.CoopSession?.BloodFeudPlayerData;
                if (data == null) return Succeeded(sb.AppendLine("Server BloodFeudPlayerData is null").ToString());

                sb.AppendLine("Server PlayerClanBloodFeuds:");
                foreach (var playerClan in data.PlayerClanBloodFeuds)
                    foreach (var feud in playerClan.Value)
                        sb.AppendLine($"  playerClan={playerClan.Key} clan={feud.Key} feud={feud.Value.HasBloodFeud} done={feud.Value.ExecutionsDoneCount} received={feud.Value.ExecutionsReceivedCount}");

                sb.AppendLine("Server PlayerClanPendingSettlementExecutions:");
                foreach (var playerClan in data.PlayerClanPendingSettlementExecutions)
                    foreach (var pending in playerClan.Value)
                        sb.AppendLine($"  playerClan={playerClan.Key} hero={pending.Key} capturerClan={pending.Value.CapturerClanId} date={new CampaignTime(pending.Value.ExecutionDateTicks)} remainingDays={new CampaignTime(pending.Value.ExecutionDateTicks).RemainingDaysFromNow:0.00}");

                sb.AppendLine("Server PlayerClanHeroesPendingMapEventEndExecution:");
                foreach (var playerClan in data.PlayerClanHeroesPendingMapEventEndExecution)
                    sb.AppendLine($"  playerClan={playerClan.Key} heroes={string.Join(",", playerClan.Value)}");
            }

            return Succeeded(sb.ToString());
        }
    }

    // coop.debug.blood_feud.take_hero_prisoner lord_1_1 lord_4_2
    /// <summary>
    /// Runs the vanilla capture of a hero by another hero's party, with patches live so it replicates.
    /// </summary>
    public sealed class BloodFeudTakeHeroPrisonerCoopCommand : ICoopCommand
    {
        private readonly ICommandObjectResolver resolver;

        public BloodFeudTakeHeroPrisonerCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.blood_feud";
        public string Name => "take_hero_prisoner";
        public string Description => "Makes a hero a prisoner of another hero's party through TakePrisonerAction.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("captor_hero", "Captor hero name or id, must lead or be in a party."),
            new ExpectedArgs("prisoner_hero", "Prisoner hero name or id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!resolver.TryResolveHero(args[0], out var captor, out var error)) return Failed(error);
            if (!resolver.TryResolveHero(args[1], out var prisoner, out error)) return Failed(error);
            if (captor.PartyBelongedTo == null) return Failed($"{captor.Name} has no party.");
            if (prisoner.IsPrisoner) return Failed($"{prisoner.Name} is already a prisoner.");
            if (prisoner.IsPlayerHero()) return Failed($"{prisoner.Name} is a player hero, use coop.debug.player_captivity.capture_player.");

            // A party leader is detached from their party first, like a defeated lord
            if (prisoner.PartyBelongedTo != null && prisoner.PartyBelongedTo.LeaderHero == prisoner)
                prisoner.PartyBelongedTo.RemovePartyLeader();

            TakePrisonerAction.Apply(captor.PartyBelongedTo.Party, prisoner);
            return Succeeded($"{prisoner.Name} ({prisoner.StringId}) clan={prisoner.Clan?.StringId} IsPrisoner={prisoner.IsPrisoner} held by {prisoner.PartyBelongedToAsPrisoner?.Name}");
        }
    }

    // coop.debug.blood_feud.settle_by_ransom clan_empire_south_1
    /// <summary>
    /// Runs the ransom broker's end feud consequence on this client for a clan, as picking it in the dialog does.
    /// </summary>
    public sealed class BloodFeudSettleByRansomCoopCommand : ICoopCommand
    {
        private readonly ICommandObjectResolver resolver;

        public BloodFeudSettleByRansomCoopCommand(ICommandObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public string Prefix => "coop.debug.blood_feud";
        public string Name => "settle_by_ransom";
        public string Description => "Runs the ransom broker end feud consequence for a clan on this client.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("clan", "Clan name or id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!resolver.TryResolveClan(args[0], out var clan, out var error)) return Failed(error);
            var behavior = Campaign.Current.GetCampaignBehavior<ExecutionCampaignBehavior>();
            if (behavior == null) return Failed("ExecutionCampaignBehavior not found.");

            behavior._selectedBloodFeudClan = clan;
            behavior.conversation_ransom_broker_end_feud_on_consequence();
            return Succeeded($"Ran end feud consequence for {clan.StringId}, local feud={clan.HasBloodFeudWithPlayer} gold={Hero.MainHero.Gold}");
        }
    }
}
