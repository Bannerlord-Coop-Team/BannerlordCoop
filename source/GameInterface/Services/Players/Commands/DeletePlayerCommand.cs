using Common.Commands;
using Common;
using Common.Messaging;
using GameInterface.Services.Players.Messages;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Players.Commands;

/// <summary>
/// Client-side command that asks the server to delete this player: the server removes the player
/// registration, kills the hero, destroys the party, and disconnects this client. Routed through
/// <see cref="Handlers.PlayerDeletionHandler"/>; a later rejoin with the same controller id goes
/// through character creation again.
/// </summary>
public class DeletePlayerCommand
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    // coop.delete_player
    /// <summary>
    /// Requests a server-authoritative deletion of the local player. Client only.
    /// </summary>

    public sealed class PlayerDeleteCoopCommand : ICoopCommand
    {
        public string Prefix => "coop";

        public string Name => "delete_player";

        public string Description => "Requests deletion of the local player from the server.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsClient) return Failed("Command can only be run on a client.");
            if (Campaign.Current == null) return Failed("No campaign is loaded.");

            MessageBroker.Instance.Publish(null, new PlayerDeleteRequested());

            return Succeeded("Delete request sent to the server. If approved, the server deletes this player's " +
                   "hero and disconnects this client; rejoining creates a new character.");
        }
    }
}
