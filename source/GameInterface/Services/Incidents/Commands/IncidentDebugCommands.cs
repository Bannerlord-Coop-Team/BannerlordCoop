using Common;
using Common.Commands;
using GameInterface.Services.Incidents.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Utils.Commands;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Incidents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.Incidents.Commands;

/// <summary>
/// Server-only commands for testing incidents without waiting for a trigger to roll.
/// The incident is sent to the player's client through the normal invoke path, so picking an option tests the full round trip.
/// </summary>
internal class IncidentDebugCommands
{
    private const string CommandPrefix = "coop.debug.incident";

    private static readonly IExpectedArgs HeroArg = new ExpectedArgs("hero", "Player hero name or id; quote names with spaces.");

    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    private static bool TryResolvePlayer(
        ICommandObjectResolver resolver,
        IPlayerManager playerManager,
        string heroQuery,
        out Player player,
        out Hero hero,
        out MobileParty party,
        out string error)
    {
        player = null;
        party = null;

        if (!resolver.TryResolveHero(heroQuery, out hero, out error)) return false;

        if (!playerManager.TryGetPlayer(hero, out player))
        {
            error = $"{hero.Name} isn't a player hero";
            return false;
        }

        return resolver.TryResolveMobileParty(player.MobilePartyId, out party, out error);
    }

    public sealed class ListIncidentsCoopCommand : ICoopCommand
    {
        private readonly ICommandObjectResolver resolver;
        private readonly IPlayerManager playerManager;
        private readonly IObjectManager objectManager;
        private readonly IIncidentsCampaignBehaviourInterface incidentsCampaignBehaviourInterface;
        private readonly ISessionIncidentsPlayerDataInterface sessionIncidentsPlayerDataInterface;

        public ListIncidentsCoopCommand(
            ICommandObjectResolver resolver,
            IPlayerManager playerManager,
            IObjectManager objectManager,
            IIncidentsCampaignBehaviourInterface incidentsCampaignBehaviourInterface,
            ISessionIncidentsPlayerDataInterface sessionIncidentsPlayerDataInterface)
        {
            this.resolver = resolver;
            this.playerManager = playerManager;
            this.objectManager = objectManager;
            this.incidentsCampaignBehaviourInterface = incidentsCampaignBehaviourInterface;
            this.sessionIncidentsPlayerDataInterface = sessionIncidentsPlayerDataInterface;
        }

        public string Prefix => CommandPrefix;

        public string Name => "list";

        public string Description => "Lists the incidents whose conditions currently hold for a player, ignoring triggers and cooldowns.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = { HeroArg };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("Run coop.debug.incident.list on the server only");
            if (args.Count < 1) return Failed("Usage: coop.debug.incident.list <hero>");

            if (!TryResolvePlayer(resolver, playerManager, args[0], out var player, out var hero, out var party, out var error)) return Failed(error);

            var incidents = incidentsCampaignBehaviourInterface.GetDebugInvokableIncidents(player, party, hero);

            var output = new StringBuilder();
            output.AppendLine($"{incidents.Count} incidents can be invoked for {hero.Name} ({player.HeroId})");
            output.AppendLine($"On global cooldown: {sessionIncidentsPlayerDataInterface.IsOnGlobalCooldown(player.HeroId)}");

            foreach (var incident in incidents)
            {
                bool isOnCooldown = objectManager.TryGetId(incident, out var incidentId)
                    && sessionIncidentsPlayerDataInterface.IsIncidentOnCooldown(player.HeroId, incidentId);

                output.AppendLine($"    {incident.StringId} - {incident.Title}{(isOnCooldown ? " (on cooldown)" : string.Empty)}");
            }

            return Succeeded(output.ToString());
        }
    }

    public sealed class InvokeIncidentCoopCommand : ICoopCommand
    {
        private readonly ICommandObjectResolver resolver;
        private readonly IPlayerManager playerManager;
        private readonly IObjectManager objectManager;
        private readonly IIncidentsCampaignBehaviourInterface incidentsCampaignBehaviourInterface;

        public InvokeIncidentCoopCommand(
            ICommandObjectResolver resolver,
            IPlayerManager playerManager,
            IObjectManager objectManager,
            IIncidentsCampaignBehaviourInterface incidentsCampaignBehaviourInterface)
        {
            this.resolver = resolver;
            this.playerManager = playerManager;
            this.objectManager = objectManager;
            this.incidentsCampaignBehaviourInterface = incidentsCampaignBehaviourInterface;
        }

        public string Prefix => CommandPrefix;

        public string Name => "invoke";

        public string Description => "Sends an incident to a player, skipping the trigger roll and cooldowns but keeping its conditions.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } =
        {
            HeroArg,
            new ExpectedArgs("incident_id", "Incident id, e.g. incident_arrow_proofing. Picks a random valid incident when left out.", isRequired: false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("Run coop.debug.incident.invoke on the server only");
            if (args.Count < 1) return Failed("Usage: coop.debug.incident.invoke <hero> [incident_id]");

            if (!TryResolvePlayer(resolver, playerManager, args[0], out var player, out var hero, out var party, out var error)) return Failed(error);

            Incident incident;
            if (args.Count >= 2)
            {
                // Accepts the vanilla StringId, since the registry id is just that with an Incident_ prefix
                if (!objectManager.TryGetObject(args[1], out incident)) return Failed($"No incident with id {args[1]}");
            }
            else
            {
                var incidents = incidentsCampaignBehaviourInterface.GetDebugInvokableIncidents(player, party, hero);
                if (incidents.Count == 0) return Failed($"No incident's conditions hold for {hero.Name}");

                incident = incidents.GetRandomElement();
            }

            if (!incidentsCampaignBehaviourInterface.TryDebugInvokeIncident(player, party, hero, incident, out error)) return Failed(error);

            return Succeeded($"Sent {incident.StringId} ({incident.Title}) to {hero.Name}");
        }
    }

    public sealed class ClearIncidentCooldownsCoopCommand : ICoopCommand
    {
        private readonly ICommandObjectResolver resolver;
        private readonly IPlayerManager playerManager;
        private readonly ISessionIncidentsPlayerDataInterface sessionIncidentsPlayerDataInterface;

        public ClearIncidentCooldownsCoopCommand(
            ICommandObjectResolver resolver,
            IPlayerManager playerManager,
            ISessionIncidentsPlayerDataInterface sessionIncidentsPlayerDataInterface)
        {
            this.resolver = resolver;
            this.playerManager = playerManager;
            this.sessionIncidentsPlayerDataInterface = sessionIncidentsPlayerDataInterface;
        }

        public string Prefix => CommandPrefix;

        public string Name => "clear_cooldowns";

        public string Description => "Clears a player's global and per-incident cooldowns so the normal triggers can roll again.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = { HeroArg };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("Run coop.debug.incident.clear_cooldowns on the server only");
            if (args.Count < 1) return Failed("Usage: coop.debug.incident.clear_cooldowns <hero>");

            if (!TryResolvePlayer(resolver, playerManager, args[0], out var player, out var hero, out _, out var error)) return Failed(error);

            sessionIncidentsPlayerDataInterface.ClearIncidentCooldowns(player.HeroId);
            sessionIncidentsPlayerDataInterface.SetGlobalCooldown(player.HeroId, CampaignTime.Zero);

            return Succeeded($"Cleared incident cooldowns for {hero.Name}");
        }
    }
}
