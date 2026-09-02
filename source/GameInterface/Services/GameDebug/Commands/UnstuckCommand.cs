using System;
using Common.Commands;
using Common;
using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Unstuck;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.GameDebug.Commands;

public class UnstuckCommand
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    // coop.debug.mobileparty.unstuck
    /// <summary>
    /// Requests a server-authoritative unstuck of the local player party. Client only.
    /// </summary>
    public sealed class UnstuckCoopCommand : ICoopCommand
    {
        public string Prefix => "coop";

        public string Name => "unstuck";

        public string Description => "Runs the unstuck debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsClient) return Failed("Command can only be run on a client.");
            if (Campaign.Current == null) return Failed("No campaign is loaded.");

            var mainParty = MobileParty.MainParty;
            if (mainParty == null) return Failed("No main party on this client.");

            MessageBroker.Instance.Publish(mainParty, new PlayerUnstuckRequested(mainParty));

            return Succeeded("Unstuck request sent to the server. Captivity, map event, army, siege camp, and " +
                   "settlement exits apply on the server; the local encounter and menu state clear when its reply arrives. " +
                   "Consenting clients may also send their current co-op log for a diagnostic report.");
        }
    }
}
