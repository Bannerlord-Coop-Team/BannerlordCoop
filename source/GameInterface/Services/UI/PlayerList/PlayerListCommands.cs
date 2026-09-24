#if DEBUG
using Common.Commands;

namespace GameInterface.Services.UI.PlayerList;

/// <summary>Opens the real map overlay for live testing without injecting operating-system input.</summary>
public sealed class TogglePlayerListCommand : ICoopCommand
{
    public string Prefix => "coop.debug.player_list";
    public string Name => "toggle";
    public string Description => "Toggles the campaign-map player list using its normal focus guards.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

    // Exercises the same action as the configured shortcut and reports whether the current screen permits it.
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!ContainerProvider.TryResolve<IPlayerListService>(out var service))
            return new CoopCommandResult(false, "Player list unavailable.", "unavailable");
        var changed = service.Toggle();
        return new CoopCommandResult(changed, service.Describe(), changed ? null : "not_on_map");
    }
}

/// <summary>Inspects the client presentation snapshot independently of the backend registry.</summary>
public sealed class InspectPlayerListCommand : ICoopCommand
{
    public string Prefix => "coop.debug.player_list";
    public string Name => "inspect";
    public string Description => "Reports the received player-list rows and open state.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

    // Reads only the data currently displayed by this client.
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!ContainerProvider.TryResolve<IPlayerListService>(out var service))
            return new CoopCommandResult(false, "Player list unavailable.", "unavailable");
        return new CoopCommandResult(true, service.Describe());
    }
}
/// <summary>Previews a large roster in the real UI without changing any campaign or network state.</summary>
public sealed class PreviewPlayerListCommand : ICoopCommand
{
    public string Prefix => "coop.debug.player_list";
    public string Name => "preview";
    public string Description => "Use on for layout rows, sorting for a compact sorting preview, off to restore the server snapshot.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[] { new ExpectedArgs("mode", "on, sorting or off") };

    // Switches only the presentation fixture; normal focus guards still control opening the list.
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (args[0] != "on" && args[0] != "sorting" && args[0] != "off")
            return new CoopCommandResult(false, "Use on, sorting or off.", "invalid_mode");
        if (!ContainerProvider.TryResolve<IPlayerListService>(out var service))
            return new CoopCommandResult(false, "Player list unavailable.", "unavailable");
        service.PreviewLayout(args[0] != "off", args[0] == "sorting");
        return new CoopCommandResult(true, service.Describe());
    }
}
#endif
