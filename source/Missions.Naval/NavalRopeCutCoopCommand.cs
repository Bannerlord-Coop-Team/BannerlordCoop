#if DEBUG
using Common.Commands;
using Newtonsoft.Json;
using System;
using System.Globalization;
using RopeState = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine.ShipAttachment.ShipAttachmentState;

namespace Missions.Naval;

/// <summary>Cuts a selected connection on the local hull authority.</summary>
public interface INavalRopeCutCoopCommand : ICoopCommand { }

/// <inheritdoc cref="INavalRopeCutCoopCommand"/>
public sealed class NavalRopeCutCoopCommand : INavalRopeCutCoopCommand
{
    private readonly INavalDebugHulls hulls;

    /// <summary>Uses the same own-hull and station ordering as rope_list and rope_throw.</summary>
    public NavalRopeCutCoopCommand(INavalDebugHulls hulls)
    {
        this.hulls = hulls;
    }

    public string Prefix => "coop.debug.naval";
    public string Name => "rope_cut";
    public string Description => "Cuts an attached own-hull rope or plank through vanilla's cut-loose path.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("source_station_index", "The own rope station from rope_list, on the client that threw it.", true),
        new ExpectedArgs("removal", "hold_removal cuts while withholding removal entries; release_removal restores normal capture.", false),
    };

    /// <summary>Rejects invalid or unconnected stations before running vanilla's disconnect with patches live.</summary>
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if ((args.Count != 1 && args.Count != 2) || !int.TryParse(args[0], NumberStyles.None, CultureInfo.InvariantCulture, out int index)
            || args.Count == 2 && args[1] != "hold_removal" && args[1] != "release_removal")
            return new CoopCommandResult(false, "Usage: coop.debug.naval.rope_cut <source_station_index> [hold_removal|release_removal]", "command_failed");

        string error = hulls.TryGetOwnHull(out var own);
        if (error != null) return new CoopCommandResult(false, error, "command_failed");

        var sources = hulls.RopeStations(own);
        if (index >= sources.Length)
            return new CoopCommandResult(false, $"The own hull has {sources.Length} rope stations.", "command_failed");

        var source = sources[index];
        if (args.Count == 2 && args[1] == "release_removal")
        {
            bool released = NavalRopes.ReleaseRemoval(source);
            return new CoopCommandResult(released, released
                ? "NAVAL_ROPE_REMOVAL_RELEASED sourceIndex=" + index.ToString(CultureInfo.InvariantCulture)
                : "This station has no held removal.", released ? null : "command_failed");
        }

        var attachment = source.CurrentAttachment;
        if (attachment == null || attachment.AttachmentTarget == null || (attachment.State != RopeState.RopesPulling
            && attachment.State != RopeState.BridgeThrown && attachment.State != RopeState.BridgeConnected))
            return new CoopCommandResult(false, "The station needs a live attached rope or plank to cut.", "command_failed");

        bool hold = args.Count == 2;
        if (hold && !NavalRopes.HoldRemoval(source))
            return new CoopCommandResult(false, "Wait for rope_status to show this live generation published, with no removal held.", "command_failed");

        // Rope decisions belong to the source hull's client authority, rather than the campaign server.
        string before = attachment.State.ToString();
        source.DisconnectAttachment();
        return new CoopCommandResult(true, "NAVAL_ROPE_CUT " + JsonConvert.SerializeObject(new
        {
            shipId = hulls.ShipIdOf(own),
            sourceIndex = index,
            key = NavalShipEngine.EntityPath(source.GameEntity, own.GameEntity),
            before,
            state = attachment.State.ToString(),
            cutUtc = DateTime.UtcNow,
            removalHeld = hold,
        }));
    }
}
#endif
