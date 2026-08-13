using Common;
using SandBox.View.Map.Managers;
using System;
using System.Collections.Generic;
using System.Text.Json;
using TaleWorlds.CampaignSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.PartyVisuals.Commands;

internal class PartyVisualDebugCommands
{
    [CommandLineArgumentFunction("buffer_state", "coop.debug.partyvisuals")]
    public static string BufferState(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";

        if (args.Count != 0)
            return "Usage: coop.debug.partyvisuals.buffer_state";

        var manager = MobilePartyVisualManager.Current;
        if (manager == null)
            return "Mobile party visual manager is unavailable.";

        int visualCount = manager._visualsFlattened.Count;
        int bufferCapacity = manager._dirtyPartiesList.Length;
        int dirtyCount = manager._dirtyPartyVisualCount;
        int campaignPartyCount = Campaign.Current?.MobileParties?.Count ?? 0;
        string structuredState = JsonSerializer.Serialize(new
        {
            visualCount,
            bufferCapacity,
            dirtyCount,
            campaignPartyCount,
        });

        return $"visualCount={visualCount} bufferCapacity={bufferCapacity} dirtyCount={dirtyCount} " +
               $"campaignPartyCount={campaignPartyCount}" + Environment.NewLine +
               $"LIVE_TEST_JSON={structuredState}";
    }
}
