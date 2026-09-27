using Common;
using Common.Commands;
using Common.Logging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace GameInterface.Services.MobileParties.Commands;

/// <summary>
/// Server console command that runs the unstuck server steps for a connected player, so an operator
/// can recover a player who cannot run coop.unstuck on their own client.
/// </summary>
internal class UnstuckPlayerCommand
{
    private static readonly ILogger Logger = LogManager.GetLogger<UnstuckPlayerCommand>();

    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    /// <summary>
    /// Exact controller id first, then a unique case-insensitive hero name. When nothing or more than
    /// one player matches, the error lists the candidates.
    /// </summary>
    internal static bool TryResolveTarget(
        string query,
        IReadOnlyCollection<Player> players,
        Func<Player, string> heroName,
        out Player target,
        out string error)
    {
        error = null;
        target = players.FirstOrDefault(player => player.ControllerId == query);
        if (target != null) return true;

        var matches = players
            .Where(player => string.Equals(heroName(player), query, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matches.Length == 1)
        {
            target = matches[0];
            return true;
        }

        var candidates = matches.Length > 1 ? matches : players;
        var listed = string.Join(", ", candidates
            .OrderBy(player => player.ControllerId, StringComparer.Ordinal)
            .Select(player => $"{player.ControllerId} ({heroName(player) ?? "<unknown hero>"})"));

        error = (matches.Length > 1
                ? $"Several players are named '{query}'; use a controller id."
                : $"No registered player has the controller id or hero name '{query}'.") +
            $" Candidates: {(listed.Length == 0 ? "none" : listed)}. coop.debug.players.list shows the current ids.";
        return false;
    }

    // coop.unstuck_player
    /// <summary>
    /// Unsticks a connected player chosen by controller id or hero name. Server only.
    /// </summary>
    public sealed class UnstuckPlayerCoopCommand : ICoopCommand
    {
        private readonly IPlayerManager playerManager;
        private readonly IObjectManager objectManager;
        private readonly IServerPlayerUnstuck serverPlayerUnstuck;
        private readonly IBattleHostRegistry battleHostRegistry;

        public UnstuckPlayerCoopCommand(
            IPlayerManager playerManager,
            IObjectManager objectManager,
            IServerPlayerUnstuck serverPlayerUnstuck,
            IBattleHostRegistry battleHostRegistry)
        {
            this.playerManager = playerManager;
            this.objectManager = objectManager;
            this.serverPlayerUnstuck = serverPlayerUnstuck;
            this.battleHostRegistry = battleHostRegistry;
        }

        public string Prefix => "coop";

        public string Name => "unstuck_player";

        public string Description => "Unsticks a connected player by controller id or hero name.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("player", "The controller id, or the hero name in quotes."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsServer) return Failed("Command can only be run on the server.");
            if (Campaign.Current == null) return Failed("No campaign is loaded.");

            if (!TryResolveTarget(args[0], playerManager.Players, GetHeroName, out var player, out var error))
                return Failed(error);

            var heroName = GetHeroName(player) ?? "<unknown hero>";
            if (!playerManager.IsConnected(player))
                return Failed($"Player {player.ControllerId} ({heroName}) is not connected; only connected players can be unstuck.");

            if (!objectManager.TryGetObject(player.MobilePartyId, out MobileParty party))
                return Failed($"Party '{player.MobilePartyId}' of player {player.ControllerId} ({heroName}) was not found on the server; nothing was applied.");

            var output = new List<string> { $"Unstuck player {player.ControllerId} (hero {heroName}, {player.HeroId}):" };

            if (TryGetHostedBattleId(party, out var mapEventId))
            {
                // Self-unstuck proceeds here too; the client's battle scene is left to the client.
                Logger.Warning("Operator unstuck for controller {ControllerId} while its party is in hosted battle {MapEventId}",
                    player.ControllerId, mapEventId);
                output.Add($"Warning: the party is in hosted battle {mapEventId}; the player's battle scene may stay open.");
            }

            var actions = serverPlayerUnstuck.Apply(player, includeCaptivity: true);
            output.AddRange(actions);

            Logger.Information("Operator unstuck for controller {ControllerId} (hero {HeroId}, party {PartyId}): {Actions}",
                player.ControllerId, player.HeroId, player.MobilePartyId, string.Join(" ", actions));

            return Succeeded(string.Join("\n", output));
        }

        private string GetHeroName(Player player)
        {
            if (string.IsNullOrEmpty(player.HeroId) ||
                !objectManager.TryGetObject(player.HeroId, out Hero hero) ||
                TextObject.IsNullOrEmpty(hero.Name))
                return null;

            return hero.Name.ToString();
        }

        private bool TryGetHostedBattleId(MobileParty party, out string mapEventId)
        {
            mapEventId = null;

            return party.Party?.MapEvent != null &&
                objectManager.TryGetId(party.Party.MapEvent, out mapEventId) &&
                battleHostRegistry.TryGet(mapEventId, out _);
        }
    }
}
