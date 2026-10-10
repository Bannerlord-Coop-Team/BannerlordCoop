using System;
using Common.Commands;
using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MobileParties;
using GameInterface.Services.MobileParties.Messages.Unstuck;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.GameDebug.Commands;

internal class UnstuckCommand
{
    private static readonly ILogger Logger = LogManager.GetLogger<UnstuckCommand>();

    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    private static CoopCommandResult InvalidArguments(string output) =>
        new CoopCommandResult(false, output, "invalid_arguments");

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

    // coop.unstuck
    /// <summary>
    /// On a client, requests a server-authoritative unstuck of the local player party. On the server,
    /// unsticks a connected player chosen by controller id or hero name.
    /// </summary>
    public sealed class UnstuckCoopCommand : ICoopCommand
    {
        private readonly IPlayerManager playerManager;
        private readonly IObjectManager objectManager;
        private readonly IServerPlayerUnstuck serverPlayerUnstuck;
        private readonly IBattleHostRegistry battleHostRegistry;

        public UnstuckCoopCommand(
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

        public string Name => "unstuck";

        public string Description => "Unsticks your own party on a client, or a connected player on the server.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("player", "Server only: the controller id, or the hero name in quotes. Leave it out on a client.", isRequired: false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (Campaign.Current == null) return Failed("No campaign is loaded.");

            return ModInformation.IsServer ? UnstuckPlayer(args) : UnstuckMainParty(args);
        }

        private CoopCommandResult UnstuckMainParty(ICoopCommandArgs args)
        {
            if (args.Count > 0)
                return InvalidArguments("On a client coop.unstuck takes no player, it always unsticks your own party. Usage: coop.unstuck");

            var mainParty = MobileParty.MainParty;
            if (mainParty == null) return Failed("No main party on this client.");

            MessageBroker.Instance.Publish(mainParty, new PlayerUnstuckRequested(mainParty));

            return Succeeded("Unstuck request sent to the server. Captivity, map event, siege camp, and " +
                   "settlement exits apply on the server. Player-led armies are preserved; followers leave their army. " +
                   "The local encounter and menu state clear when its reply arrives. " +
                   "Consenting clients may also send their current co-op log for a diagnostic report.");
        }

        private CoopCommandResult UnstuckPlayer(ICoopCommandArgs args)
        {
            if (args.Count == 0)
                return InvalidArguments("On the server coop.unstuck needs the player to unstick. " +
                    "Usage: coop.unstuck <controller id or \"hero name\">. coop.debug.players.list shows the current ids.");

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
