using Common;
using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Unstuck;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.MobileParties.Commands;

/// <summary>
/// Client-side recovery command that forces the local player party out of stuck states —
/// captivity, army membership, siege camp, settlement, and player encounter. Routed through the
/// dedicated unstuck flow (<see cref="Handlers.PlayerUnstuckHandler"/>): the server force-applies
/// the synced exits and its reply clears the local encounter/menu state, so recovery works even
/// when a normal exit request flow is what is wedged.
/// </summary>
public class UnstuckCommand
{
    // coop.debug.mobileparty.unstuck
    /// <summary>
    /// Requests a server-authoritative unstuck of the local player party. Client only.
    /// </summary>
    [CommandLineArgumentFunction("unstuck", "coop.debug.mobileparty")]
    public static string Unstuck(List<string> args)
    {
        if (!ModInformation.IsClient) return "Command can only be run on a client.";
        if (Campaign.Current == null) return "No campaign is loaded.";

        var mainParty = MobileParty.MainParty;
        if (mainParty == null) return "No main party on this client.";

        MessageBroker.Instance.Publish(mainParty, new PlayerUnstuckRequested(mainParty));

        return "Unstuck request sent to the server. Captivity, army, siege camp, and settlement exits " +
               "apply on the server; the local encounter and menu state clear when its reply arrives.";
    }
}
