using Common.Commands;
using Common;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases.Extensions;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Players.Commands;

internal class PlayerDebugCommands
{
    private static CoopCommandResult Succeeded(string output) =>

        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>

        new CoopCommandResult(false, output, "command_failed");

    // coop.debug.players.list

    public sealed class PlayerListCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.players";

        public string Name => "list";

        public string Description => "Lists registered co-op players.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) == false)
                return Failed($"Unable to get {nameof(IPlayerManager)}");
            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
                return Failed($"Unable to get {nameof(IObjectManager)}");

            ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
            var localId = controllerIdProvider?.ControllerId;

            var players = playerManager.Players;

            var sb = new StringBuilder();
            sb.AppendLine($"Side: {(ModInformation.IsServer ? "Server" : "Client")}  LocalControllerId: {localId ?? "<unknown>"}");
            sb.AppendLine($"Registered players: {players.Count} (expected: one per client, host excluded)");

            // PlayerObjects (the hero/party/clan -> controller table) is not enumerable, so report it indirectly:
            // each player contributes its resolvable, registered hero/party/clan. The total should be 3x the
            // player count once everyone's objects are present.
            int controlledObjects = 0;

            foreach (var player in players)
            {
                var marker = player.ControllerId == localId ? " (you)" : "";
                sb.AppendLine($"- ControllerId: {player.ControllerId}{marker}");
                controlledObjects += AppendObject<Hero>(sb, objectManager, playerManager, "Hero", player.HeroId);
                controlledObjects += AppendObject<MobileParty>(sb, objectManager, playerManager, "Party", player.MobilePartyId);
                controlledObjects += AppendObject<Clan>(sb, objectManager, playerManager, "Clan", player.ClanId);
            }

            sb.AppendLine($"PlayerObjects entries (resolved & controlled): {controlledObjects}");

            return Succeeded(sb.ToString());
        }
    }

    public sealed class PlayerPartyStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.players";

        public string Name => "party_state";

        public string Description => "Reports replicated party state for a player.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The player controller id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsServer)
                return Failed("Command can only be run on the server.");
            if (ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) == false)
                return Failed($"Unable to get {nameof(IPlayerManager)}");
            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
                return Failed($"Unable to get {nameof(IObjectManager)}");
            if (!playerManager.TryGetPlayer(args[0], out var player))
                return Failed($"Controller {args[0]} is not registered.");
            if (!objectManager.TryGetObject(player.MobilePartyId, out MobileParty party))
                return Failed($"Party {player.MobilePartyId} is not resolved.");

            string mapEventId = party.MapEvent?.StringId ?? "none";
            bool hasVisual = party.Party.GetPartyVisual() != null;
            string structuredState = JsonConvert.SerializeObject(new
            {
                controllerId = player.ControllerId,
                partyId = player.MobilePartyId,
                partyStringId = party.StringId,
                connected = playerManager.IsConnected(player),
                active = party.IsActive,
                mapEvent = mapEventId,
                visual = hasVisual,
            });

            return
                Succeeded($"controller={player.ControllerId}|" +
                $"party={party.StringId}|" +
                $"connected={playerManager.IsConnected(player)}|" +
                $"active={party.IsActive}|" +
                $"mapEvent={mapEventId}|" +
                $"visual={hasVisual}" +
                $"\nLIVE_TEST_JSON={structuredState}");
        }
    }

    /// <summary>
    /// Reports one of a player's controlled ids: whether it resolves and whether it is in the
    /// PlayerManager's control table. Returns 1 when both hold, otherwise 0.
    /// </summary>
    private static int AppendObject<T>(
        StringBuilder sb,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        string label,
        string id) where T : class
    {
        if (string.IsNullOrEmpty(id))
        {
            sb.AppendLine($"    {label}: <none>");
            return 0;
        }

        if (objectManager.TryGetObject<T>(id, out var obj) == false)
        {
            sb.AppendLine($"    {label}: {id} <NOT RESOLVED>");
            return 0;
        }

        bool controlled = playerManager.Contains(obj);
        sb.AppendLine($"    {label}: {id} resolved, controlled={controlled}");
        return controlled ? 1 : 0;
    }
}
