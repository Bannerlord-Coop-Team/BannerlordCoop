using Common;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Players.Commands;

internal class PlayerDebugCommands
{
    // coop.debug.players.list
    [CommandLineArgumentFunction("list", "coop.debug.players")]
    public static string List(List<string> args)
    {
        if (ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) == false)
            return $"Unable to get {nameof(IPlayerManager)}";
        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            return $"Unable to get {nameof(IObjectManager)}";

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

        return sb.ToString();
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
