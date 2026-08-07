using Common;
using Common.Messaging;
using GameInterface.Services.Players.Messages;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Players.Commands;

/// <summary>
/// Client-side command that asks the server to delete this player: the server removes the player
/// registration, kills the hero, destroys the party, and disconnects this client. Routed through
/// <see cref="Handlers.PlayerDeletionHandler"/>; a later rejoin with the same controller id goes
/// through character creation again.
/// </summary>
public class DeletePlayerCommand
{
    // coop.delete_player
    /// <summary>
    /// Requests a server-authoritative deletion of the local player. Client only.
    /// </summary>
    [CommandLineArgumentFunction("delete_player", "coop")]
    public static string DeletePlayer(List<string> args)
    {
        if (!ModInformation.IsClient) return "Command can only be run on a client.";
        if (Campaign.Current == null) return "No campaign is loaded.";

        MessageBroker.Instance.Publish(null, new PlayerDeleteRequested());

        return "Delete request sent to the server. If approved, the server deletes this player's " +
               "hero and disconnects this client; rejoining creates a new character.";
    }
}
